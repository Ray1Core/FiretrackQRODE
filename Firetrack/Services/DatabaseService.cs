using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Firetrack.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

namespace Firetrack.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly bool _useSqlServer;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
            _useSqlServer = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                            (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
                             !connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase));

            System.Diagnostics.Debug.WriteLine($"ℹ️ DatabaseService mode: {(_useSqlServer ? "SQL Server" : "SQLite")}");
            InitializeDatabase();
        }

        private IDbConnection CreateConnection()
        {
            return _useSqlServer ? new SqlConnection(_connectionString) : new SqliteConnection(_connectionString);
        }

        private string LastInsertIdFunction => _useSqlServer ? "SCOPE_IDENTITY()" : "last_insert_rowid()";
        private string DateTimeNowFunction => _useSqlServer ? "GETDATE()" : "CURRENT_TIMESTAMP";

        private void InitializeDatabase()
        {
#if ANDROID
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
#endif
            if (!_useSqlServer)
            {
                var dbPath = _connectionString.Replace("Data Source=", "").Trim();
                var directory = System.IO.Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);
            }

            using var connection = CreateConnection();
            connection.Open();

            if (_useSqlServer) CreateTablesSqlServer(connection);
            else CreateTablesSqlite(connection);

            MigrateEquipmentAddMissingColumns(connection);
            SeedData(connection);
        }

        // ============================================================
        // SAFETY MIGRATION (adds missing columns to existing Equipment tables)
        // ============================================================
        private void MigrateEquipmentAddMissingColumns(IDbConnection connection)
        {
            List<string> existingColumns;
            if (_useSqlServer)
                existingColumns = connection.Query<string>("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Equipment'").ToList();
            else
                existingColumns = connection.Query<string>("SELECT name FROM pragma_table_info('Equipment')").ToList();

            void Add(string col, string sType, string mType)
            {
                if (!existingColumns.Contains(col))
                {
                    connection.Execute($"ALTER TABLE Equipment ADD COLUMN {col} {(_useSqlServer ? mType : sType)}");
                    System.Diagnostics.Debug.WriteLine($"✅ Added column '{col}' to Equipment table");
                }
            }

            Add("AssignedToUsername", "TEXT NULL", "NVARCHAR(255) NULL");
            Add("RequestedByUsername", "TEXT NULL", "NVARCHAR(255) NULL");
            Add("RequestStatus", "TEXT NULL", "NVARCHAR(30) NULL");
            Add("IsDisposalRequested", "INTEGER DEFAULT 0", "BIT DEFAULT 0");
            Add("DisposalStatus", "TEXT NULL", "NVARCHAR(30) NULL");
            Add("DisposalReason", "TEXT NULL", "NVARCHAR(MAX) NULL");
            Add("DisposalRequestedBy", "TEXT NULL", "NVARCHAR(255) NULL");
            Add("DisposalRequestDate", "DATETIME NULL", "DATETIME NULL");
            Add("DisposalApprovedBy", "TEXT NULL", "NVARCHAR(255) NULL");
            Add("DisposalApprovalDate", "DATETIME NULL", "DATETIME NULL");
            Add("DisposalRemarks", "TEXT NULL", "NVARCHAR(MAX) NULL");
            Add("PhotoPath", "TEXT NULL", "NVARCHAR(500) NULL");

            // ✅ NEW: Clean up stale assignment records left over from the old Return flow.
            // Any assignment still marked 'Assigned' for an item that is Available or Disposed
            // is a leftover and should be closed out.
            try
            {
                int cleaned = connection.Execute(@"
            UPDATE Assignments 
            SET AssignmentStatus = 'Returned', ReturnedDate = @Date
            WHERE AssignmentStatus = 'Assigned' 
              AND EquipmentId IN (
                  SELECT EquipmentId FROM Equipment 
                  WHERE ConditionStatus = 'Available' OR ConditionStatus = 'Disposed'
              )", new { Date = DateTime.Now.Date });

                if (cleaned > 0)
                    System.Diagnostics.Debug.WriteLine($"✅ Cleaned up {cleaned} stale assignment record(s)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Stale assignment cleanup skipped: {ex.Message}");
            }
        }

        // ============================================================
        // CREATE TABLES — SQLITE
        // ============================================================
        private void CreateTablesSqlite(IDbConnection connection)
        {
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL, Action TEXT NOT NULL,
                    Details TEXT NULL, Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP)");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS PasswordResetOtps (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL, OtpCode TEXT NOT NULL,
                    Expiry DATETIME NOT NULL, IsUsed INTEGER NOT NULL DEFAULT 0)");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Roles (
                    RoleId INTEGER PRIMARY KEY AUTOINCREMENT, RoleName TEXT NOT NULL UNIQUE,
                    Description TEXT, CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP)");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Users (
                    UserId INTEGER PRIMARY KEY AUTOINCREMENT, RoleId INTEGER NOT NULL,
                    FirstName TEXT NOT NULL, LastName TEXT NOT NULL, Email TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL, Status TEXT DEFAULT 'Active',
                    ProfileImagePath TEXT NULL, PersonalQR TEXT NULL,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP, UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId))");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Equipment (
                    EquipmentId INTEGER PRIMARY KEY AUTOINCREMENT, PropertyNumber TEXT NOT NULL UNIQUE,
                    ItemName TEXT NOT NULL, Category TEXT NOT NULL, Description TEXT, SerialNumber TEXT,
                    AcquisitionDate DATE, AcquisitionCost DECIMAL(12,2), ConditionStatus TEXT DEFAULT 'Available',
                    AssignedToUsername TEXT NULL, RequestedByUsername TEXT NULL, RequestStatus TEXT NULL,
                    IsDisposalRequested INTEGER DEFAULT 0, DisposalStatus TEXT NULL, DisposalReason TEXT NULL,
                    DisposalRequestedBy TEXT NULL, DisposalRequestDate DATETIME NULL, DisposalApprovedBy TEXT NULL,
                    DisposalApprovalDate DATETIME NULL, DisposalRemarks TEXT NULL, PhotoPath TEXT NULL,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP, UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP)");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Requests (
                    RequestId INTEGER PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL, EquipmentId INTEGER NOT NULL,
                    Quantity INTEGER NOT NULL DEFAULT 1, Purpose TEXT NOT NULL, RequestStatus TEXT DEFAULT 'Pending',
                    RequestedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users(UserId), FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId))");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Assignments (
                    AssignmentId INTEGER PRIMARY KEY AUTOINCREMENT, EquipmentId INTEGER NOT NULL, UserId INTEGER NOT NULL,
                    AssignedDate DATE NOT NULL, ReturnedDate DATE NULL, AssignmentStatus TEXT DEFAULT 'Assigned',
                    Remarks TEXT, CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId), FOREIGN KEY (UserId) REFERENCES Users(UserId))");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Handshakes (
                    HandshakeId INTEGER PRIMARY KEY AUTOINCREMENT, EquipmentId INTEGER NOT NULL, FromUserId INTEGER NOT NULL,
                    ToUserId INTEGER NOT NULL, TransferDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    Status TEXT DEFAULT 'Pending', Notes TEXT,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (FromUserId) REFERENCES Users(UserId), FOREIGN KEY (ToUserId) REFERENCES Users(UserId))");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS DamageReports (
                    ReportId INTEGER PRIMARY KEY AUTOINCREMENT, EquipmentId INTEGER NOT NULL, ReportedBy INTEGER NOT NULL,
                    IncidentDate DATE NOT NULL, DamageDescription TEXT NOT NULL, ReportStatus TEXT DEFAULT 'Reported',
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId), FOREIGN KEY (ReportedBy) REFERENCES Users(UserId))");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS DisposalRequests (
                    DisposalId INTEGER PRIMARY KEY AUTOINCREMENT, EquipmentId INTEGER NOT NULL, RequestedBy INTEGER NOT NULL,
                    Reason TEXT NOT NULL, DisposalStatus TEXT DEFAULT 'Pending Review',
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId), FOREIGN KEY (RequestedBy) REFERENCES Users(UserId))");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Notifications (
                    NotificationId INTEGER PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL,
                    Title TEXT NOT NULL, Message TEXT NOT NULL, IsRead INTEGER DEFAULT 0,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY (UserId) REFERENCES Users(UserId))");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS IcsDocuments (
                    IcsId INTEGER PRIMARY KEY AUTOINCREMENT, EquipmentId INTEGER NOT NULL, IssuedTo INTEGER NOT NULL,
                    IcsNumber TEXT NOT NULL UNIQUE, DateIssued DATE NOT NULL, DocumentPath TEXT,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId), FOREIGN KEY (IssuedTo) REFERENCES Users(UserId))");

            System.Diagnostics.Debug.WriteLine("✅ SQLite tables created/verified.");
        }

        // ============================================================
        // CREATE TABLES — SQL SERVER
        // ============================================================
        private void CreateTablesSqlServer(IDbConnection connection)
        {
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditLogs' AND xtype='U')
                CREATE TABLE AuditLogs (Id INT PRIMARY KEY IDENTITY(1,1), Username NVARCHAR(255) NOT NULL,
                    Action NVARCHAR(255) NOT NULL, Details NVARCHAR(MAX) NULL, Timestamp DATETIME NOT NULL DEFAULT GETDATE())");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasswordResetOtps' AND xtype='U')
                CREATE TABLE PasswordResetOtps (Id INT PRIMARY KEY IDENTITY(1,1), Username NVARCHAR(255) NOT NULL,
                    OtpCode NVARCHAR(10) NOT NULL, Expiry DATETIME NOT NULL, IsUsed INT NOT NULL DEFAULT 0)");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Roles' AND xtype='U')
                CREATE TABLE Roles (RoleId INT PRIMARY KEY IDENTITY(1,1), RoleName NVARCHAR(50) NOT NULL UNIQUE,
                    Description NVARCHAR(255), CreatedAt DATETIME DEFAULT GETDATE())");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                CREATE TABLE Users (UserId INT PRIMARY KEY IDENTITY(1,1), RoleId INT NOT NULL, FirstName NVARCHAR(100) NOT NULL,
                    LastName NVARCHAR(100) NOT NULL, Email NVARCHAR(255) NOT NULL UNIQUE, PasswordHash NVARCHAR(255) NOT NULL,
                    Status NVARCHAR(20) DEFAULT 'Active', ProfileImagePath NVARCHAR(500) NULL, PersonalQR NVARCHAR(100) NULL,
                    CreatedAt DATETIME DEFAULT GETDATE(), UpdatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId))");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Equipment' AND xtype='U')
                CREATE TABLE Equipment (EquipmentId INT PRIMARY KEY IDENTITY(1,1), PropertyNumber NVARCHAR(100) NOT NULL UNIQUE,
                    ItemName NVARCHAR(255) NOT NULL, Category NVARCHAR(100) NOT NULL, Description NVARCHAR(MAX), SerialNumber NVARCHAR(100),
                    AcquisitionDate DATE, AcquisitionCost DECIMAL(12,2), ConditionStatus NVARCHAR(50) DEFAULT 'Available',
                    AssignedToUsername NVARCHAR(255) NULL, RequestedByUsername NVARCHAR(255) NULL, RequestStatus NVARCHAR(30) NULL,
                    IsDisposalRequested BIT DEFAULT 0, DisposalStatus NVARCHAR(30) NULL, DisposalReason NVARCHAR(MAX) NULL,
                    DisposalRequestedBy NVARCHAR(255) NULL, DisposalRequestDate DATETIME NULL, DisposalApprovedBy NVARCHAR(255) NULL,
                    DisposalApprovalDate DATETIME NULL, DisposalRemarks NVARCHAR(MAX) NULL, PhotoPath NVARCHAR(500) NULL,
                    CreatedAt DATETIME DEFAULT GETDATE(), UpdatedAt DATETIME DEFAULT GETDATE())");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Requests' AND xtype='U')
                CREATE TABLE Requests (RequestId INT PRIMARY KEY IDENTITY(1,1), UserId INT NOT NULL, EquipmentId INT NOT NULL,
                    Quantity INT NOT NULL DEFAULT 1, Purpose NVARCHAR(MAX) NOT NULL, RequestStatus NVARCHAR(20) DEFAULT 'Pending',
                    RequestedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (UserId) REFERENCES Users(UserId), FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId))");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Assignments' AND xtype='U')
                CREATE TABLE Assignments (AssignmentId INT PRIMARY KEY IDENTITY(1,1), EquipmentId INT NOT NULL, UserId INT NOT NULL,
                    AssignedDate DATE NOT NULL, ReturnedDate DATE NULL, AssignmentStatus NVARCHAR(20) DEFAULT 'Assigned',
                    Remarks NVARCHAR(MAX), CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId), FOREIGN KEY (UserId) REFERENCES Users(UserId))");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Handshakes' AND xtype='U')
                CREATE TABLE Handshakes (HandshakeId INT PRIMARY KEY IDENTITY(1,1), EquipmentId INT NOT NULL, FromUserId INT NOT NULL,
                    ToUserId INT NOT NULL, TransferDate DATETIME DEFAULT GETDATE(), Status NVARCHAR(20) DEFAULT 'Pending', Notes NVARCHAR(MAX),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (FromUserId) REFERENCES Users(UserId), FOREIGN KEY (ToUserId) REFERENCES Users(UserId))");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DamageReports' AND xtype='U')
                CREATE TABLE DamageReports (ReportId INT PRIMARY KEY IDENTITY(1,1), EquipmentId INT NOT NULL, ReportedBy INT NOT NULL,
                    IncidentDate DATE NOT NULL, DamageDescription NVARCHAR(MAX) NOT NULL, ReportStatus NVARCHAR(30) DEFAULT 'Reported',
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId), FOREIGN KEY (ReportedBy) REFERENCES Users(UserId))");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DisposalRequests' AND xtype='U')
                CREATE TABLE DisposalRequests (DisposalId INT PRIMARY KEY IDENTITY(1,1), EquipmentId INT NOT NULL, RequestedBy INT NOT NULL,
                    Reason NVARCHAR(MAX) NOT NULL, DisposalStatus NVARCHAR(30) DEFAULT 'Pending Review',
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId), FOREIGN KEY (RequestedBy) REFERENCES Users(UserId))");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Notifications' AND xtype='U')
                CREATE TABLE Notifications (NotificationId INT PRIMARY KEY IDENTITY(1,1), UserId INT NOT NULL,
                    Title NVARCHAR(255) NOT NULL, Message NVARCHAR(MAX) NOT NULL, IsRead BIT DEFAULT 0,
                    CreatedAt DATETIME DEFAULT GETDATE(), FOREIGN KEY (UserId) REFERENCES Users(UserId))");
            connection.Execute(@"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='IcsDocuments' AND xtype='U')
                CREATE TABLE IcsDocuments (IcsId INT PRIMARY KEY IDENTITY(1,1), EquipmentId INT NOT NULL, IssuedTo INT NOT NULL,
                    IcsNumber NVARCHAR(100) NOT NULL UNIQUE, DateIssued DATE NOT NULL, DocumentPath NVARCHAR(500),
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId), FOREIGN KEY (IssuedTo) REFERENCES Users(UserId))");

            System.Diagnostics.Debug.WriteLine("✅ SQL Server tables created/verified.");
        }

        // ============================================================
        // SEED DATA
        // ============================================================
        private void SeedData(IDbConnection connection)
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Roles") == 0)
            {
                connection.Execute(@"INSERT INTO Roles (RoleName, Description) VALUES
                    ('Admin', 'System administrator with full access'),
                    ('Personnel', 'Firefighter / regular user')");
            }

            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Users") == 0)
            {
                var adminRoleId = connection.ExecuteScalar<int>("SELECT RoleId FROM Roles WHERE RoleName = 'Admin'");
                var personnelRoleId = connection.ExecuteScalar<int>("SELECT RoleId FROM Roles WHERE RoleName = 'Personnel'");

                connection.Execute(@"INSERT INTO Users (RoleId, FirstName, LastName, Email, PasswordHash, Status, ProfileImagePath, PersonalQR) VALUES
                    (@AdminRole, 'Admin', 'Chief', 'admin@firetrack.gov', 'admin123', 'Active', NULL, 'ADMIN-001'),
                    (@PersonnelRole, 'John', 'Firefighter', 'john@firetrack.gov', 'user123', 'Active', NULL, 'PERSON-001')",
                    new { AdminRole = adminRoleId, PersonnelRole = personnelRoleId });
            }

            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Equipment") == 0)
            {
                connection.Execute(@"
                    INSERT INTO Equipment (PropertyNumber, ItemName, Category, Description, SerialNumber, AcquisitionDate, AcquisitionCost, ConditionStatus) VALUES
                    ('HOSE001', 'Fire Hose 1.5 x 15m', 'Hose', 'Standard fire hose', NULL, '2023-01-01', 150.00, 'Available'),
                    ('HOSE002', 'Fire Hose 2.5 x 15m', 'Hose', 'Heavy duty hose', NULL, '2023-01-15', 200.00, 'Available'),
                    ('HOSE003', 'Fire Hose 2.5 x 30m', 'Hose', 'Long length hose', NULL, '2023-02-01', 300.00, 'Available'),
                    ('NOZZLE001', 'Combination Nozzle', 'Nozzle', 'Multi-purpose nozzle', NULL, '2023-03-01', 80.00, 'Available'),
                    ('NOZZLE002', 'Fog Nozzle', 'Nozzle', 'Fog pattern nozzle', NULL, '2023-03-15', 75.00, 'Available'),
                    ('TOOL001', 'Halligan Tool', 'Rescue Tool', 'Multipurpose forcible entry tool', NULL, '2023-04-01', 120.00, 'Available'),
                    ('TOOL002', 'Flathead Axe', 'Rescue Tool', 'Fire axe', NULL, '2023-04-15', 90.00, 'Available'),
                    ('TOOL003', 'Pry Bar', 'Rescue Tool', 'Pry bar for rescue', NULL, '2023-05-01', 60.00, 'Available'),
                    ('TOOL004', 'Bolt Cutter', 'Rescue Tool', 'Heavy duty bolt cutter', NULL, '2023-05-15', 110.00, 'Available'),
                    ('TOOL005', 'Search & Rescue Rope', 'Rescue Tool', 'Rope for search and rescue', NULL, '2023-06-01', 50.00, 'Available')");
            }

            var johnUser = connection.QueryFirstOrDefault<UserModel>("SELECT * FROM Users WHERE Email = 'john@firetrack.gov'");
            var hose1 = connection.QueryFirstOrDefault<EquipmentModel>("SELECT * FROM Equipment WHERE PropertyNumber = 'HOSE001'");
            var hose3 = connection.QueryFirstOrDefault<EquipmentModel>("SELECT * FROM Equipment WHERE PropertyNumber = 'HOSE003'");
            var tool3 = connection.QueryFirstOrDefault<EquipmentModel>("SELECT * FROM Equipment WHERE PropertyNumber = 'TOOL003'");

            if (johnUser != null && hose1 != null && hose3 != null && tool3 != null)
            {
                if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Assignments") == 0)
                {
                    connection.Execute(@"
                        INSERT INTO Assignments (EquipmentId, UserId, AssignedDate, AssignmentStatus) VALUES
                        (@Eq1, @User, @Date, 'Assigned'),
                        (@Eq2, @User, @Date, 'Assigned'),
                        (@Eq3, @User, @Date, 'Assigned')",
                        new
                        {
                            Eq1 = hose3.EquipmentId,
                            Eq2 = tool3.EquipmentId,
                            Eq3 = hose1.EquipmentId,
                            User = johnUser.UserId,
                            Date = DateTime.Now.Date
                        });

                    // ✅ Mark these items as Issued and set AssignedToUsername
                    connection.Execute(@"
                        UPDATE Equipment 
                        SET ConditionStatus = 'Issued', AssignedToUsername = @Username 
                        WHERE EquipmentId IN (@Eq1, @Eq2, @Eq3)",
                        new
                        {
                            Eq1 = hose3.EquipmentId,
                            Eq2 = tool3.EquipmentId,
                            Eq3 = hose1.EquipmentId,
                            Username = johnUser.Email
                        });
                }
            }
        }

        // ============================================================
        // AUDIT LOGS
        // ============================================================
        public async Task LogActionAsync(string username, string action, string? details = null)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                $"INSERT INTO AuditLogs (Username, Action, Details, Timestamp) VALUES (@Username, @Action, @Details, {DateTimeNowFunction})",
                new { Username = username, Action = action, Details = details });
        }

        public async Task<List<AuditLogModel>> GetAuditLogsAsync()
        {
            using var connection = CreateConnection();
            return (await connection.QueryAsync<AuditLogModel>("SELECT * FROM AuditLogs ORDER BY Timestamp DESC")).ToList();
        }

        // ============================================================
        // OTPs
        // ============================================================
        public async Task<string> GenerateOtpAsync(string username)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync($"DELETE FROM PasswordResetOtps WHERE Username = @Username OR Expiry < {DateTimeNowFunction}", new { Username = username });
            var otpCode = new Random().Next(100000, 999999).ToString();
            await connection.ExecuteAsync("INSERT INTO PasswordResetOtps (Username, OtpCode, Expiry, IsUsed) VALUES (@Username, @OtpCode, @Expiry, 0)",
                new { Username = username, OtpCode = otpCode, Expiry = DateTime.Now.AddMinutes(10) });
            return otpCode;
        }

        public async Task<bool> ValidateOtpAsync(string username, string otpCode)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<OtpModel>(
                $"SELECT * FROM PasswordResetOtps WHERE Username=@Username AND OtpCode=@OtpCode AND IsUsed=0 AND Expiry > {DateTimeNowFunction}",
                new { Username = username, OtpCode = otpCode }) != null;
        }

        public async Task MarkOtpUsedAsync(string username, string otpCode)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync("UPDATE PasswordResetOtps SET IsUsed=1 WHERE Username=@Username AND OtpCode=@OtpCode",
                new { Username = username, OtpCode = otpCode });
        }

        // ============================================================
        // USERS
        // ============================================================
        public async Task<UserModel?> GetUserByUsernameAsync(string username)
        {
            using var connection = CreateConnection();
            var user = await connection.QueryFirstOrDefaultAsync<UserModel>("SELECT * FROM Users WHERE Email=@Username", new { Username = username });
            if (user != null)
            {
                var role = await connection.QueryFirstOrDefaultAsync("SELECT RoleName FROM Roles WHERE RoleId=@RoleId", new { user.RoleId });
                user.Role = role?.RoleName ?? "Personnel";
            }
            return user;
        }

        public async Task<UserModel?> GetUserByEmailAsync(string email) => await GetUserByUsernameAsync(email);

        public async Task<int> SaveUserAsync(UserModel user)
        {
            using var connection = CreateConnection();
            if (user.RoleId == 0 && !string.IsNullOrEmpty(user.Role))
            {
                var roleId = await connection.ExecuteScalarAsync<int>("SELECT RoleId FROM Roles WHERE RoleName=@RoleName", new { RoleName = user.Role });
                user.RoleId = roleId > 0 ? roleId : 2;
            }
            if (user.UserId == 0 && string.IsNullOrEmpty(user.PersonalQR))
                user.PersonalQR = $"{(user.Role ?? "PERSON").ToUpper()}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

            string sql = user.UserId == 0
                ? $@"INSERT INTO Users (RoleId, FirstName, LastName, Email, PasswordHash, Status, ProfileImagePath, PersonalQR, UpdatedAt)
                     VALUES (@RoleId, @FirstName, @LastName, @Email, @PasswordHash, @Status, @ProfileImagePath, @PersonalQR, {DateTimeNowFunction});
                     SELECT {LastInsertIdFunction};"
                : $@"UPDATE Users SET RoleId=@RoleId, FirstName=@FirstName, LastName=@LastName, Email=@Email,
                        PasswordHash=@PasswordHash, Status=@Status, ProfileImagePath=@ProfileImagePath, PersonalQR=@PersonalQR,
                        UpdatedAt={DateTimeNowFunction} WHERE UserId=@UserId; SELECT @UserId;";
            return await connection.ExecuteScalarAsync<int>(sql, user);
        }

        public async Task<List<UserModel>> GetUsersAsync()
        {
            using var connection = CreateConnection();
            var users = (await connection.QueryAsync<UserModel>("SELECT * FROM Users")).ToList();
            foreach (var user in users)
            {
                var role = await connection.QueryFirstOrDefaultAsync("SELECT RoleName FROM Roles WHERE RoleId=@RoleId", new { user.RoleId });
                user.Role = role?.RoleName ?? "Personnel";
            }
            return users;
        }

        public async Task<int> UpdateUserAsync(UserModel user)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync($@"UPDATE Users SET RoleId=@RoleId, FirstName=@FirstName, LastName=@LastName,
                Email=@Email, PasswordHash=@PasswordHash, Status=@Status, ProfileImagePath=@ProfileImagePath,
                PersonalQR=@PersonalQR, UpdatedAt={DateTimeNowFunction} WHERE UserId=@UserId", user);
        }

        public async Task<bool> ResetPasswordAsync(string username, string newPassword)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync("UPDATE Users SET PasswordHash=@Password WHERE Email=@Username",
                new { Password = newPassword, Username = username }) > 0;
        }

        // ============================================================
        // NOTIFICATIONS
        // ============================================================
        public async Task<int> SaveNotificationAsync(NotificationModel notification)
        {
            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(notification.Username);
            if (user == null) return 0;
            return await connection.ExecuteScalarAsync<int>(
                $@"INSERT INTO Notifications (UserId, Title, Message, IsRead, CreatedAt)
                   VALUES (@UserId, @Title, @Message, @IsRead, {DateTimeNowFunction});
                   SELECT {LastInsertIdFunction};",
                new { UserId = user.UserId, notification.Title, notification.Message, notification.IsRead });
        }

        public async Task<List<NotificationModel>> GetNotificationsForUserAsync(string username)
        {
            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(username);
            if (user == null) return new List<NotificationModel>();
            return (await connection.QueryAsync<NotificationModel>(
                @"SELECT NotificationId, UserId, Title, Message, IsRead, CreatedAt as Timestamp
                  FROM Notifications WHERE UserId=@UserId ORDER BY CreatedAt DESC",
                new { UserId = user.UserId })).ToList();
        }

        public async Task<int> MarkNotificationAsReadAsync(int id)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync("UPDATE Notifications SET IsRead=1 WHERE NotificationId=@Id", new { Id = id });
        }

        public async Task<int> MarkAllNotificationsAsReadAsync(string username)
        {
            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(username);
            if (user == null) return 0;
            return await connection.ExecuteAsync("UPDATE Notifications SET IsRead=1 WHERE UserId=@UserId", new { UserId = user.UserId });
        }

        public async Task SendNotificationAsync(string username, string title, string message)
        {
            await SaveNotificationAsync(new NotificationModel { Username = username, Title = title, Message = message, IsRead = false, Timestamp = DateTime.Now });
        }

        // ============================================================
        // EQUIPMENT
        // ============================================================
        public async Task<List<EquipmentModel>> GetEquipmentsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<EquipmentModel>("SELECT * FROM Equipment");
            return result.ToList();
        }

        public async Task<List<EquipmentModel>> GetEquipmentsAssignedToUserAsync(string username)
        {
            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(username);
            if (user == null) return new List<EquipmentModel>();

            // ✅ FIX: Only return items that are actively in the user's custody.
            // Assignment must be 'Assigned' AND the item must still be held
            // (Issued, Damaged, or InRepair). Available/Disposed items are excluded.
            var sql = @"SELECT e.* FROM Equipment e
                JOIN Assignments a ON e.EquipmentId = a.EquipmentId
                WHERE a.UserId = @UserId 
                  AND a.AssignmentStatus = 'Assigned'
                  AND e.ConditionStatus IN ('Issued', 'Damaged', 'InRepair')";
            var result = (await connection.QueryAsync<EquipmentModel>(sql, new { UserId = user.UserId })).ToList();

            foreach (var eq in result)
            {
                if (string.IsNullOrEmpty(eq.AssignedToUsername))
                    eq.AssignedToUsername = username;
            }
            return result;
        }

        public async Task<int> SaveEquipmentAsync(EquipmentModel equipment)
        {
            using var connection = CreateConnection();
            string sql;
            if (equipment.EquipmentId == 0)
            {
                sql = $@"INSERT INTO Equipment (PropertyNumber, ItemName, Category, Description, SerialNumber,
                            AcquisitionDate, AcquisitionCost, ConditionStatus, AssignedToUsername, RequestedByUsername,
                            RequestStatus, IsDisposalRequested, DisposalStatus, DisposalReason, DisposalRequestedBy,
                            DisposalRequestDate, DisposalApprovedBy, DisposalApprovalDate, DisposalRemarks, PhotoPath, UpdatedAt)
                         VALUES (@PropertyNumber, @ItemName, @Category, @Description, @SerialNumber,
                            @AcquisitionDate, @AcquisitionCost, @ConditionStatus, @AssignedToUsername, @RequestedByUsername,
                            @RequestStatus, @IsDisposalRequested, @DisposalStatus, @DisposalReason, @DisposalRequestedBy,
                            @DisposalRequestDate, @DisposalApprovedBy, @DisposalApprovalDate, @DisposalRemarks, @PhotoPath, {DateTimeNowFunction});
                         SELECT {LastInsertIdFunction};";
            }
            else
            {
                sql = $@"UPDATE Equipment SET PropertyNumber=@PropertyNumber, ItemName=@ItemName, Category=@Category,
                            Description=@Description, SerialNumber=@SerialNumber, AcquisitionDate=@AcquisitionDate,
                            AcquisitionCost=@AcquisitionCost, ConditionStatus=@ConditionStatus,
                            AssignedToUsername=@AssignedToUsername, RequestedByUsername=@RequestedByUsername,
                            RequestStatus=@RequestStatus, IsDisposalRequested=@IsDisposalRequested,
                            DisposalStatus=@DisposalStatus, DisposalReason=@DisposalReason,
                            DisposalRequestedBy=@DisposalRequestedBy, DisposalRequestDate=@DisposalRequestDate,
                            DisposalApprovedBy=@DisposalApprovedBy, DisposalApprovalDate=@DisposalApprovalDate,
                            DisposalRemarks=@DisposalRemarks, PhotoPath=@PhotoPath, UpdatedAt={DateTimeNowFunction}
                         WHERE EquipmentId=@EquipmentId; SELECT @EquipmentId;";
            }
            return await connection.ExecuteScalarAsync<int>(sql, equipment);
        }

        public async Task<int> DeleteEquipmentAsync(EquipmentModel equipment)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync("DELETE FROM Equipment WHERE EquipmentId = @EquipmentId", equipment);
        }

        public async Task<EquipmentModel?> GetEquipmentByQRAsync(string qrCode)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<EquipmentModel>(
                "SELECT * FROM Equipment WHERE PropertyNumber = @QRCode", new { QRCode = qrCode });
        }

        public async Task<EquipmentModel?> GetEquipmentByPropertyNumberAsync(string propertyNumber)
            => await GetEquipmentByQRAsync(propertyNumber);

        // ============================================================
        // REQUESTS
        // ============================================================
        public async Task<List<EquipmentModel>> GetPendingRequestsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<EquipmentModel>(
                "SELECT * FROM Equipment WHERE RequestStatus = 'Pending'");
            return result.ToList();
        }

        public async Task<int> ApproveRequestAsync(string qrCode, UserModel approver)
        {
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;

            var user = await GetUserByUsernameAsync(equipment.RequestedByUsername!);
            if (user == null) return 0;

            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                @"INSERT INTO Assignments (EquipmentId, UserId, AssignedDate, AssignmentStatus)
                  VALUES (@EquipmentId, @UserId, @Date, 'Assigned')",
                new { EquipmentId = equipment.EquipmentId, UserId = user.UserId, Date = DateTime.Now.Date });

            equipment.ConditionStatus = "Issued";
            equipment.AssignedToUsername = user.Username;
            equipment.RequestStatus = "Approved";
            equipment.RequestedByUsername = null;
            equipment.LastUpdated = DateTime.Now;

            await SaveEquipmentAsync(equipment);
            await SendNotificationAsync(user.Username, "✅ Request Approved",
                $"Your request for '{equipment.ItemName}' has been approved.");
            return 1;
        }

        public async Task<int> RejectRequestAsync(string qrCode, UserModel approver)
        {
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;

            var requestedBy = equipment.RequestedByUsername;
            equipment.RequestedByUsername = null;
            equipment.RequestStatus = null;
            equipment.LastUpdated = DateTime.Now;
            await SaveEquipmentAsync(equipment);

            if (!string.IsNullOrEmpty(requestedBy))
            {
                await SendNotificationAsync(requestedBy, "❌ Request Rejected",
                    $"Your request for '{equipment.ItemName}' has been rejected.");
            }
            return 1;
        }

        public async Task<int> UpdateRequestStatusAsync(string qrCode, string status, string? approver = null)
        {
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;
            equipment.RequestStatus = status;
            equipment.LastUpdated = DateTime.Now;
            return await SaveEquipmentAsync(equipment);
        }

        // ============================================================
        // RETURN
        // ============================================================
        public async Task<bool> ReturnEquipmentAsync(string qrCode, string username)
        {
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return false;

            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(username);
            if (user != null)
            {
                await connection.ExecuteAsync(@"
                    UPDATE Assignments SET AssignmentStatus = 'Returned', ReturnedDate = @Date
                    WHERE EquipmentId = @EquipmentId AND UserId = @UserId AND AssignmentStatus = 'Assigned'",
                    new { EquipmentId = equipment.EquipmentId, UserId = user.UserId, Date = DateTime.Now.Date });
            }

            equipment.ConditionStatus = "Available";
            equipment.AssignedToUsername = null;
            equipment.LastUpdated = DateTime.Now;
            await SaveEquipmentAsync(equipment);

            await LogActionAsync(username, "Return Equipment", $"Returned '{equipment.ItemName}' ({equipment.PropertyNumber})");
            return true;
        }

        // ============================================================
        // TRANSACTIONS
        // ============================================================
        public async Task<int> SaveTransactionAsync(TransactionModel transaction)
        {
            await LogActionAsync(transaction.FromUser, transaction.Action,
                $"Equipment {transaction.EquipmentQR}: {transaction.FromUser} -> {transaction.ToUser}. Remarks: {transaction.Remarks}");
            return 1;
        }

        public async Task<List<TransactionModel>> GetTransactionsForEquipmentAsync(string qrCode)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<TransactionModel>(
                @"SELECT -1 as TransactionId, @QRCode as EquipmentQR, Username as FromUser, 'System' as ToUser,
                         Timestamp, Action, Details as Remarks
                  FROM AuditLogs WHERE Details LIKE @Pattern ORDER BY Timestamp DESC",
                new { QRCode = qrCode, Pattern = $"%{qrCode}%" });
            return result.ToList();
        }

        public async Task<List<TransactionModel>> GetTransactionsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<TransactionModel>(
                @"SELECT -1 as TransactionId, 'N/A' as EquipmentQR, Username as FromUser, 'System' as ToUser,
                         Timestamp, Action, Details as Remarks FROM AuditLogs ORDER BY Timestamp DESC");
            return result.ToList();
        }

        // ============================================================
        // DISPOSAL
        // ============================================================
        public async Task<List<EquipmentModel>> GetDisposalRequestsAsync(string? status = "Pending Review")
        {
            using var connection = CreateConnection();
            var sql = @"SELECT e.*, dr.Reason AS DisposalReason, dr.DisposalStatus AS DisposalStatus,
                               u.Email AS DisposalRequestedBy, dr.CreatedAt AS DisposalRequestDate
                        FROM Equipment e
                        JOIN DisposalRequests dr ON e.EquipmentId = dr.EquipmentId
                        JOIN Users u ON dr.RequestedBy = u.UserId
                        WHERE dr.DisposalStatus = @Status
                        ORDER BY dr.CreatedAt DESC";
            var result = await connection.QueryAsync<EquipmentModel>(sql, new { Status = status });
            return result.ToList();
        }

        public async Task<bool> RequestDisposalAsync(string qrCode, string requestedBy, string reason)
        {
            using var connection = CreateConnection();
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return false;
            var user = await GetUserByUsernameAsync(requestedBy);
            if (user == null) return false;

            await connection.ExecuteAsync(
                @"INSERT INTO DisposalRequests (EquipmentId, RequestedBy, Reason, DisposalStatus)
                  VALUES (@EquipmentId, @RequestedBy, @Reason, 'Pending Review')",
                new { EquipmentId = equipment.EquipmentId, RequestedBy = user.UserId, Reason = reason });

            equipment.IsDisposalRequested = true;
            equipment.DisposalStatus = "Pending Review";
            equipment.DisposalReason = reason;
            equipment.DisposalRequestedBy = requestedBy;
            equipment.DisposalRequestDate = DateTime.Now;
            equipment.LastUpdated = DateTime.Now;
            await SaveEquipmentAsync(equipment);

            await SendNotificationAsync("admin@firetrack.gov", "🗑️ Disposal Request",
                $"{requestedBy} requested disposal for '{equipment.ItemName}' (QR: {equipment.PropertyNumber})");
            return true;
        }

        public async Task<bool> ApproveDisposalAsync(string qrCode, string approvedBy, string remarks = "")
        {
            using var connection = CreateConnection();
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return false;

            await connection.ExecuteAsync(
                "UPDATE DisposalRequests SET DisposalStatus = 'Approved' WHERE EquipmentId = @EquipmentId AND DisposalStatus = 'Pending Review'",
                new { EquipmentId = equipment.EquipmentId });

            equipment.ConditionStatus = "Disposed";
            equipment.DisposalStatus = "Approved";
            equipment.DisposalApprovedBy = approvedBy;
            equipment.DisposalApprovalDate = DateTime.Now;
            equipment.DisposalRemarks = remarks;
            equipment.AssignedToUsername = null;
            equipment.LastUpdated = DateTime.Now;
            await SaveEquipmentAsync(equipment);

            if (!string.IsNullOrEmpty(equipment.DisposalRequestedBy))
                await SendNotificationAsync(equipment.DisposalRequestedBy, "✅ Disposal Approved",
                    $"Disposal of '{equipment.ItemName}' has been approved.");
            return true;
        }

        public async Task<bool> RejectDisposalAsync(string qrCode, string rejectedBy, string remarks = "")
        {
            using var connection = CreateConnection();
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return false;

            await connection.ExecuteAsync(
                "UPDATE DisposalRequests SET DisposalStatus = 'Rejected' WHERE EquipmentId = @EquipmentId AND DisposalStatus = 'Pending Review'",
                new { EquipmentId = equipment.EquipmentId });

            equipment.IsDisposalRequested = false;
            equipment.DisposalStatus = "Rejected";
            equipment.DisposalRemarks = remarks;
            equipment.LastUpdated = DateTime.Now;
            await SaveEquipmentAsync(equipment);

            if (!string.IsNullOrEmpty(equipment.DisposalRequestedBy))
                await SendNotificationAsync(equipment.DisposalRequestedBy, "❌ Disposal Rejected",
                    $"Disposal of '{equipment.ItemName}' was rejected.");
            return true;
        }

        // ============================================================
        // HANDSHAKES
        // ============================================================
        public async Task<int> CreateHandshakeAsync(int equipmentId, int fromUserId, int toUserId, string notes = "")
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(
                $@"INSERT INTO Handshakes (EquipmentId, FromUserId, ToUserId, TransferDate, Status, Notes)
                   VALUES (@EquipmentId, @FromUserId, @ToUserId, {DateTimeNowFunction}, 'Pending', @Notes);
                   SELECT {LastInsertIdFunction};",
                new { EquipmentId = equipmentId, FromUserId = fromUserId, ToUserId = toUserId, Notes = notes });
        }

        public async Task<bool> AcceptHandshakeAsync(int handshakeId)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync("UPDATE Handshakes SET Status='Accepted' WHERE HandshakeId=@Id AND Status='Pending'",
                new { Id = handshakeId }) > 0;
        }

        public async Task<List<HandshakeModel>> GetPendingHandshakesForUserAsync(int userId)
        {
            using var connection = CreateConnection();
            return (await connection.QueryAsync<HandshakeModel>("SELECT * FROM Handshakes WHERE ToUserId=@UserId AND Status='Pending'",
                new { UserId = userId })).ToList();
        }

        // ============================================================
        // DAMAGE REPORTS
        // ============================================================
        public async Task<int> SaveDamageReportAsync(DamageReportModel report)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(
                $@"INSERT INTO DamageReports (EquipmentId, ReportedBy, IncidentDate, DamageDescription, ReportStatus, CreatedAt)
                   VALUES (@EquipmentId, @ReportedBy, @IncidentDate, @DamageDescription, @ReportStatus, {DateTimeNowFunction});
                   SELECT {LastInsertIdFunction};", report);
        }

        public async Task<List<DamageReportModel>> GetDamageReportsForEquipmentAsync(int equipmentId)
        {
            using var connection = CreateConnection();
            return (await connection.QueryAsync<DamageReportModel>("SELECT * FROM DamageReports WHERE EquipmentId=@Id ORDER BY CreatedAt DESC",
                new { Id = equipmentId })).ToList();
        }

        public async Task<List<DamageReportModel>> GetAllDamageReportsAsync()
        {
            using var connection = CreateConnection();
            return (await connection.QueryAsync<DamageReportModel>("SELECT * FROM DamageReports ORDER BY CreatedAt DESC")).ToList();
        }

        public async Task<int> UpdateDamageReportStatusAsync(int reportId, string status)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync("UPDATE DamageReports SET ReportStatus=@Status WHERE ReportId=@Id", new { Status = status, Id = reportId });
        }

        // ============================================================
        // ICS DOCUMENTS
        // ============================================================
        public async Task<int> SaveIcsDocumentAsync(IcsDocumentModel ics)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(
                $@"INSERT INTO IcsDocuments (EquipmentId, IssuedTo, IcsNumber, DateIssued, DocumentPath, CreatedAt)
                   VALUES (@EquipmentId, @IssuedTo, @IcsNumber, @DateIssued, @DocumentPath, {DateTimeNowFunction});
                   SELECT {LastInsertIdFunction};", ics);
        }

        public async Task<IcsDocumentModel?> GetIcsByNumberAsync(string icsNumber)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<IcsDocumentModel>("SELECT * FROM IcsDocuments WHERE IcsNumber=@N", new { N = icsNumber });
        }

        public async Task<List<IcsDocumentModel>> GetIcsDocumentsForEquipmentAsync(int equipmentId)
        {
            using var connection = CreateConnection();
            return (await connection.QueryAsync<IcsDocumentModel>("SELECT * FROM IcsDocuments WHERE EquipmentId=@Id ORDER BY CreatedAt DESC", new { Id = equipmentId })).ToList();
        }

        public async Task<List<IcsDocumentModel>> GetIcsDocumentsForUserAsync(int userId)
        {
            using var connection = CreateConnection();
            return (await connection.QueryAsync<IcsDocumentModel>("SELECT * FROM IcsDocuments WHERE IssuedTo=@Id ORDER BY CreatedAt DESC", new { Id = userId })).ToList();
        }
    }
}