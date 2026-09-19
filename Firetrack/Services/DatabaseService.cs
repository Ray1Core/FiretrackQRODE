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

            // Detect SQL Server vs SQLite
            _useSqlServer = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                            (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
                             !connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase));

            System.Diagnostics.Debug.WriteLine($"ℹ️ DatabaseService mode: {(_useSqlServer ? "SQL Server" : "SQLite")}");

            try
            {
                InitializeDatabase();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Database initialization failed: {ex}");
                throw;
            }
        }

        private IDbConnection CreateConnection()
        {
            if (_useSqlServer)
                return new SqlConnection(_connectionString);
            else
                return new SqliteConnection(_connectionString);
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

            if (_useSqlServer)
                CreateTablesSqlServer(connection);
            else
            {
                CreateTablesSqlite(connection);
                MigrateEquipmentTableIfNeeded(connection);
            }

            SeedData(connection);
        }

        // ============================================================
        // MIGRATION (SQLite only)
        // ============================================================
        private void MigrateEquipmentTableIfNeeded(IDbConnection connection)
        {
            var createSql = connection.QueryFirstOrDefault<string>(
                "SELECT sql FROM sqlite_master WHERE type='table' AND name='Equipment'");
            if (string.IsNullOrEmpty(createSql))
                return;

            if (createSql.Contains("CHECK (ConditionStatus IN ('Serviceable','Unserviceable','Under Repair','Disposed'))"))
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Migrating Equipment table – removing CHECK constraint...");

                connection.Execute(@"
                    CREATE TABLE Equipment_new (
                        EquipmentId INTEGER PRIMARY KEY AUTOINCREMENT,
                        PropertyNumber TEXT NOT NULL UNIQUE,
                        ItemName TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        Description TEXT,
                        SerialNumber TEXT,
                        AcquisitionDate DATE,
                        AcquisitionCost DECIMAL(12,2),
                        ConditionStatus TEXT DEFAULT 'Available',
                        CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    )");

                connection.Execute(@"
                    INSERT INTO Equipment_new (
                        EquipmentId, PropertyNumber, ItemName, Category, Description,
                        SerialNumber, AcquisitionDate, AcquisitionCost, ConditionStatus,
                        CreatedAt, UpdatedAt
                    )
                    SELECT
                        EquipmentId, PropertyNumber, ItemName, Category, Description,
                        SerialNumber, AcquisitionDate, AcquisitionCost, ConditionStatus,
                        CreatedAt, UpdatedAt
                    FROM Equipment");

                connection.Execute("DROP TABLE Equipment");
                connection.Execute("ALTER TABLE Equipment_new RENAME TO Equipment");
                System.Diagnostics.Debug.WriteLine("✅ Equipment table migrated successfully.");
            }
        }

        // ============================================================
        // CREATE TABLES — SQLITE SYNTAX
        // ============================================================
        private void CreateTablesSqlite(IDbConnection connection)
        {
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL,
                    Action TEXT NOT NULL,
                    Details TEXT NULL,
                    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS PasswordResetOtps (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL,
                    OtpCode TEXT NOT NULL,
                    Expiry DATETIME NOT NULL,
                    IsUsed INTEGER NOT NULL DEFAULT 0
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Roles (
                    RoleId INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoleName TEXT NOT NULL UNIQUE,
                    Description TEXT,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Users (
                    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoleId INTEGER NOT NULL,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    Email TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Status TEXT CHECK(Status IN ('Active', 'Inactive', 'Suspended')) DEFAULT 'Active',
                    ProfileImagePath TEXT NULL,
                    PersonalQR TEXT NULL,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Equipment (
                    EquipmentId INTEGER PRIMARY KEY AUTOINCREMENT,
                    PropertyNumber TEXT NOT NULL UNIQUE,
                    ItemName TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Description TEXT,
                    SerialNumber TEXT,
                    AcquisitionDate DATE,
                    AcquisitionCost DECIMAL(12,2),
                    ConditionStatus TEXT DEFAULT 'Available',
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Requests (
                    RequestId INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    EquipmentId INTEGER NOT NULL,
                    Quantity INTEGER NOT NULL DEFAULT 1,
                    Purpose TEXT NOT NULL,
                    RequestStatus TEXT CHECK(RequestStatus IN ('Pending', 'Approved', 'Rejected')) DEFAULT 'Pending',
                    RequestedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users(UserId),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId)
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Assignments (
                    AssignmentId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EquipmentId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    AssignedDate DATE NOT NULL,
                    ReturnedDate DATE NULL,
                    AssignmentStatus TEXT CHECK(AssignmentStatus IN ('Assigned', 'Returned', 'Transferred')) DEFAULT 'Assigned',
                    Remarks TEXT,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (UserId) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Handshakes (
                    HandshakeId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EquipmentId INTEGER NOT NULL,
                    FromUserId INTEGER NOT NULL,
                    ToUserId INTEGER NOT NULL,
                    TransferDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    Status TEXT CHECK(Status IN ('Pending', 'Accepted', 'Rejected')) DEFAULT 'Pending',
                    Notes TEXT,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (FromUserId) REFERENCES Users(UserId),
                    FOREIGN KEY (ToUserId) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS DamageReports (
                    ReportId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EquipmentId INTEGER NOT NULL,
                    ReportedBy INTEGER NOT NULL,
                    IncidentDate DATE NOT NULL,
                    DamageDescription TEXT NOT NULL,
                    ReportStatus TEXT CHECK(ReportStatus IN ('Reported', 'Under Inspection', 'Resolved', 'For Disposal')) DEFAULT 'Reported',
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (ReportedBy) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS DisposalRequests (
                    DisposalId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EquipmentId INTEGER NOT NULL,
                    RequestedBy INTEGER NOT NULL,
                    Reason TEXT NOT NULL,
                    DisposalStatus TEXT CHECK(DisposalStatus IN ('Pending Review', 'Approved', 'Completed', 'Rejected')) DEFAULT 'Pending Review',
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (RequestedBy) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Notifications (
                    NotificationId INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    IsRead INTEGER DEFAULT 0,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS IcsDocuments (
                    IcsId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EquipmentId INTEGER NOT NULL,
                    IssuedTo INTEGER NOT NULL,
                    IcsNumber TEXT NOT NULL UNIQUE,
                    DateIssued DATE NOT NULL,
                    DocumentPath TEXT,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (IssuedTo) REFERENCES Users(UserId)
                )");

            System.Diagnostics.Debug.WriteLine("✅ SQLite tables created/verified.");
        }

        // ============================================================
        // CREATE TABLES — SQL SERVER SYNTAX
        // ============================================================
        private void CreateTablesSqlServer(IDbConnection connection)
        {
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditLogs' AND xtype='U')
                CREATE TABLE AuditLogs (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    Username NVARCHAR(255) NOT NULL,
                    Action NVARCHAR(255) NOT NULL,
                    Details NVARCHAR(MAX) NULL,
                    Timestamp DATETIME NOT NULL DEFAULT GETDATE()
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasswordResetOtps' AND xtype='U')
                CREATE TABLE PasswordResetOtps (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    Username NVARCHAR(255) NOT NULL,
                    OtpCode NVARCHAR(10) NOT NULL,
                    Expiry DATETIME NOT NULL,
                    IsUsed INT NOT NULL DEFAULT 0
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Roles' AND xtype='U')
                CREATE TABLE Roles (
                    RoleId INT PRIMARY KEY IDENTITY(1,1),
                    RoleName NVARCHAR(50) NOT NULL UNIQUE,
                    Description NVARCHAR(255),
                    CreatedAt DATETIME DEFAULT GETDATE()
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                CREATE TABLE Users (
                    UserId INT PRIMARY KEY IDENTITY(1,1),
                    RoleId INT NOT NULL,
                    FirstName NVARCHAR(100) NOT NULL,
                    LastName NVARCHAR(100) NOT NULL,
                    Email NVARCHAR(255) NOT NULL UNIQUE,
                    PasswordHash NVARCHAR(255) NOT NULL,
                    Status NVARCHAR(20) DEFAULT 'Active',
                    ProfileImagePath NVARCHAR(500) NULL,
                    PersonalQR NVARCHAR(100) NULL,
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    UpdatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Equipment' AND xtype='U')
                CREATE TABLE Equipment (
                    EquipmentId INT PRIMARY KEY IDENTITY(1,1),
                    PropertyNumber NVARCHAR(100) NOT NULL UNIQUE,
                    ItemName NVARCHAR(255) NOT NULL,
                    Category NVARCHAR(100) NOT NULL,
                    Description NVARCHAR(MAX),
                    SerialNumber NVARCHAR(100),
                    AcquisitionDate DATE,
                    AcquisitionCost DECIMAL(12,2),
                    ConditionStatus NVARCHAR(50) DEFAULT 'Available',
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    UpdatedAt DATETIME DEFAULT GETDATE()
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Requests' AND xtype='U')
                CREATE TABLE Requests (
                    RequestId INT PRIMARY KEY IDENTITY(1,1),
                    UserId INT NOT NULL,
                    EquipmentId INT NOT NULL,
                    Quantity INT NOT NULL DEFAULT 1,
                    Purpose NVARCHAR(MAX) NOT NULL,
                    RequestStatus NVARCHAR(20) DEFAULT 'Pending',
                    RequestedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (UserId) REFERENCES Users(UserId),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId)
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Assignments' AND xtype='U')
                CREATE TABLE Assignments (
                    AssignmentId INT PRIMARY KEY IDENTITY(1,1),
                    EquipmentId INT NOT NULL,
                    UserId INT NOT NULL,
                    AssignedDate DATE NOT NULL,
                    ReturnedDate DATE NULL,
                    AssignmentStatus NVARCHAR(20) DEFAULT 'Assigned',
                    Remarks NVARCHAR(MAX),
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (UserId) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Handshakes' AND xtype='U')
                CREATE TABLE Handshakes (
                    HandshakeId INT PRIMARY KEY IDENTITY(1,1),
                    EquipmentId INT NOT NULL,
                    FromUserId INT NOT NULL,
                    ToUserId INT NOT NULL,
                    TransferDate DATETIME DEFAULT GETDATE(),
                    Status NVARCHAR(20) DEFAULT 'Pending',
                    Notes NVARCHAR(MAX),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (FromUserId) REFERENCES Users(UserId),
                    FOREIGN KEY (ToUserId) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DamageReports' AND xtype='U')
                CREATE TABLE DamageReports (
                    ReportId INT PRIMARY KEY IDENTITY(1,1),
                    EquipmentId INT NOT NULL,
                    ReportedBy INT NOT NULL,
                    IncidentDate DATE NOT NULL,
                    DamageDescription NVARCHAR(MAX) NOT NULL,
                    ReportStatus NVARCHAR(30) DEFAULT 'Reported',
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (ReportedBy) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DisposalRequests' AND xtype='U')
                CREATE TABLE DisposalRequests (
                    DisposalId INT PRIMARY KEY IDENTITY(1,1),
                    EquipmentId INT NOT NULL,
                    RequestedBy INT NOT NULL,
                    Reason NVARCHAR(MAX) NOT NULL,
                    DisposalStatus NVARCHAR(30) DEFAULT 'Pending Review',
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (RequestedBy) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Notifications' AND xtype='U')
                CREATE TABLE Notifications (
                    NotificationId INT PRIMARY KEY IDENTITY(1,1),
                    UserId INT NOT NULL,
                    Title NVARCHAR(255) NOT NULL,
                    Message NVARCHAR(MAX) NOT NULL,
                    IsRead BIT DEFAULT 0,
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (UserId) REFERENCES Users(UserId)
                )");

            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='IcsDocuments' AND xtype='U')
                CREATE TABLE IcsDocuments (
                    IcsId INT PRIMARY KEY IDENTITY(1,1),
                    EquipmentId INT NOT NULL,
                    IssuedTo INT NOT NULL,
                    IcsNumber NVARCHAR(100) NOT NULL UNIQUE,
                    DateIssued DATE NOT NULL,
                    DocumentPath NVARCHAR(500),
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (EquipmentId) REFERENCES Equipment(EquipmentId),
                    FOREIGN KEY (IssuedTo) REFERENCES Users(UserId)
                )");

            System.Diagnostics.Debug.WriteLine("✅ SQL Server tables created/verified.");
        }

        // ============================================================
        // SEED DATA (works for both dialects)
        // ============================================================
        private void SeedData(IDbConnection connection)
        {
            int roleCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Roles");
            if (roleCount == 0)
            {
                connection.Execute(@"
                    INSERT INTO Roles (RoleName, Description) VALUES
                    ('Admin', 'System administrator with full access'),
                    ('Personnel', 'Firefighter / regular user')");
            }

            int userCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Users");
            if (userCount == 0)
            {
                var adminRoleId = connection.ExecuteScalar<int>("SELECT RoleId FROM Roles WHERE RoleName = 'Admin'");
                var personnelRoleId = connection.ExecuteScalar<int>("SELECT RoleId FROM Roles WHERE RoleName = 'Personnel'");

                connection.Execute(@"
                    INSERT INTO Users (RoleId, FirstName, LastName, Email, PasswordHash, Status, ProfileImagePath, PersonalQR) VALUES
                    (@AdminRole, 'Admin', 'Chief', 'admin@firetrack.gov', 'admin123', 'Active', NULL, 'ADMIN-001'),
                    (@PersonnelRole, 'John', 'Firefighter', 'john@firetrack.gov', 'user123', 'Active', NULL, 'PERSON-001')",
                    new { AdminRole = adminRoleId, PersonnelRole = personnelRoleId });
            }

            int eqCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Equipment");
            if (eqCount == 0)
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

            var adminUser = connection.QueryFirstOrDefault<UserModel>("SELECT * FROM Users WHERE Email = 'admin@firetrack.gov'");
            var johnUser = connection.QueryFirstOrDefault<UserModel>("SELECT * FROM Users WHERE Email = 'john@firetrack.gov'");
            var hose1 = connection.QueryFirstOrDefault<EquipmentModel>("SELECT * FROM Equipment WHERE PropertyNumber = 'HOSE001'");
            var hose3 = connection.QueryFirstOrDefault<EquipmentModel>("SELECT * FROM Equipment WHERE PropertyNumber = 'HOSE003'");
            var tool3 = connection.QueryFirstOrDefault<EquipmentModel>("SELECT * FROM Equipment WHERE PropertyNumber = 'TOOL003'");

            if (adminUser != null && johnUser != null && hose1 != null && hose3 != null && tool3 != null)
            {
                int assignCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Assignments");
                if (assignCount == 0)
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
                }
            }
        }

        // ============================================================
        // AUDIT LOGS
        // ============================================================
        public async Task LogActionAsync(string username, string action, string? details = null)
        {
            using var connection = CreateConnection();
            string sql = $"INSERT INTO AuditLogs (Username, Action, Details, Timestamp) VALUES (@Username, @Action, @Details, {DateTimeNowFunction})";
            await connection.ExecuteAsync(sql, new { Username = username, Action = action, Details = details });
        }

        public async Task<List<AuditLogModel>> GetAuditLogsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<AuditLogModel>("SELECT * FROM AuditLogs ORDER BY Timestamp DESC");
            return result.ToList();
        }

        // ============================================================
        // OTPs
        // ============================================================
        public async Task<string> GenerateOtpAsync(string username)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                $"DELETE FROM PasswordResetOtps WHERE Username = @Username OR Expiry < {DateTimeNowFunction}",
                new { Username = username });

            var random = new Random();
            string otpCode = random.Next(100000, 999999).ToString();
            var expiry = DateTime.Now.AddMinutes(10);

            await connection.ExecuteAsync(
                "INSERT INTO PasswordResetOtps (Username, OtpCode, Expiry, IsUsed) VALUES (@Username, @OtpCode, @Expiry, 0)",
                new { Username = username, OtpCode = otpCode, Expiry = expiry });

            return otpCode;
        }

        public async Task<bool> ValidateOtpAsync(string username, string otpCode)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<OtpModel>(
                $"SELECT * FROM PasswordResetOtps WHERE Username = @Username AND OtpCode = @OtpCode AND IsUsed = 0 AND Expiry > {DateTimeNowFunction}",
                new { Username = username, OtpCode = otpCode });
            return result != null;
        }

        public async Task MarkOtpUsedAsync(string username, string otpCode)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                "UPDATE PasswordResetOtps SET IsUsed = 1 WHERE Username = @Username AND OtpCode = @OtpCode",
                new { Username = username, OtpCode = otpCode });
        }

        // ============================================================
        // USERS
        // ============================================================
        public async Task<UserModel?> GetUserByUsernameAsync(string username)
        {
            using var connection = CreateConnection();
            var user = await connection.QueryFirstOrDefaultAsync<UserModel>(
                "SELECT * FROM Users WHERE Email = @Username", new { Username = username });

            if (user != null)
            {
                var role = await connection.QueryFirstOrDefaultAsync(
                    "SELECT RoleName FROM Roles WHERE RoleId = @RoleId", new { user.RoleId });
                user.Role = role?.RoleName ?? "Personnel";
            }
            return user;
        }

        public async Task<UserModel?> GetUserByEmailAsync(string email)
        {
            return await GetUserByUsernameAsync(email);
        }

        public async Task<int> SaveUserAsync(UserModel user)
        {
            using var connection = CreateConnection();

            if (user.RoleId == 0 && !string.IsNullOrEmpty(user.Role))
            {
                var roleId = await connection.ExecuteScalarAsync<int>(
                    "SELECT RoleId FROM Roles WHERE RoleName = @RoleName", new { RoleName = user.Role });
                user.RoleId = roleId > 0 ? roleId : 2;
            }

            if (user.UserId == 0 && string.IsNullOrEmpty(user.PersonalQR))
            {
                var role = user.Role ?? "PERSON";
                var suffix = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                user.PersonalQR = $"{role.ToUpper()}-{suffix}";
            }

            string sql;
            if (user.UserId == 0)
            {
                sql = $@"INSERT INTO Users (RoleId, FirstName, LastName, Email, PasswordHash, Status, ProfileImagePath, PersonalQR, UpdatedAt)
                         VALUES (@RoleId, @FirstName, @LastName, @Email, @PasswordHash, @Status, @ProfileImagePath, @PersonalQR, {DateTimeNowFunction});
                         SELECT {LastInsertIdFunction};";
            }
            else
            {
                sql = $@"UPDATE Users SET RoleId = @RoleId, FirstName = @FirstName, LastName = @LastName,
                            Email = @Email, PasswordHash = @PasswordHash, Status = @Status,
                            ProfileImagePath = @ProfileImagePath, PersonalQR = @PersonalQR,
                            UpdatedAt = {DateTimeNowFunction}
                         WHERE UserId = @UserId;
                         SELECT @UserId;";
            }

            return await connection.ExecuteScalarAsync<int>(sql, user);
        }

        public async Task<List<UserModel>> GetUsersAsync()
        {
            using var connection = CreateConnection();
            var users = await connection.QueryAsync<UserModel>("SELECT * FROM Users");
            var result = users.ToList();

            foreach (var user in result)
            {
                var role = await connection.QueryFirstOrDefaultAsync(
                    "SELECT RoleName FROM Roles WHERE RoleId = @RoleId", new { user.RoleId });
                user.Role = role?.RoleName ?? "Personnel";
            }
            return result;
        }

        public async Task<int> UpdateUserAsync(UserModel user)
        {
            using var connection = CreateConnection();
            string sql = $@"UPDATE Users SET RoleId = @RoleId, FirstName = @FirstName, LastName = @LastName,
                                Email = @Email, PasswordHash = @PasswordHash, Status = @Status,
                                ProfileImagePath = @ProfileImagePath, PersonalQR = @PersonalQR,
                                UpdatedAt = {DateTimeNowFunction}
                            WHERE UserId = @UserId";
            return await connection.ExecuteAsync(sql, user);
        }

        public async Task<bool> ResetPasswordAsync(string username, string newPassword)
        {
            using var connection = CreateConnection();
            int rows = await connection.ExecuteAsync(
                "UPDATE Users SET PasswordHash = @Password WHERE Email = @Username",
                new { Password = newPassword, Username = username });
            return rows > 0;
        }

        // ============================================================
        // NOTIFICATIONS
        // ============================================================
        public async Task<int> SaveNotificationAsync(NotificationModel notification)
        {
            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(notification.Username);
            if (user == null) return 0;

            string sql = $@"INSERT INTO Notifications (UserId, Title, Message, IsRead, CreatedAt)
                            VALUES (@UserId, @Title, @Message, @IsRead, {DateTimeNowFunction});
                            SELECT {LastInsertIdFunction};";
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                UserId = user.UserId,
                notification.Title,
                notification.Message,
                notification.IsRead
            });
        }

        public async Task<List<NotificationModel>> GetNotificationsForUserAsync(string username)
        {
            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(username);
            if (user == null) return new List<NotificationModel>();

            var result = await connection.QueryAsync<NotificationModel>(
                @"SELECT NotificationId, UserId, Title, Message, IsRead, CreatedAt as Timestamp
                  FROM Notifications WHERE UserId = @UserId ORDER BY CreatedAt DESC",
                new { UserId = user.UserId });
            return result.ToList();
        }

        public async Task<int> MarkNotificationAsReadAsync(int notificationId)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE NotificationId = @NotificationId",
                new { NotificationId = notificationId });
        }

        public async Task<int> MarkAllNotificationsAsReadAsync(string username)
        {
            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(username);
            if (user == null) return 0;
            return await connection.ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE UserId = @UserId",
                new { UserId = user.UserId });
        }

        public async Task SendNotificationAsync(string username, string title, string message)
        {
            await SaveNotificationAsync(new NotificationModel
            {
                Username = username,
                Title = title,
                Message = message,
                IsRead = false,
                Timestamp = DateTime.Now
            });
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

            var sql = @"SELECT e.* FROM Equipment e
                        JOIN Assignments a ON e.EquipmentId = a.EquipmentId
                        WHERE a.UserId = @UserId AND a.AssignmentStatus = 'Assigned'";
            var result = await connection.QueryAsync<EquipmentModel>(sql, new { UserId = user.UserId });
            return result.ToList();
        }

        public async Task<int> SaveEquipmentAsync(EquipmentModel equipment)
        {
            using var connection = CreateConnection();
            string sql;
            if (equipment.EquipmentId == 0)
            {
                sql = $@"INSERT INTO Equipment (PropertyNumber, ItemName, Category, Description, SerialNumber, AcquisitionDate, AcquisitionCost, ConditionStatus, UpdatedAt)
                         VALUES (@PropertyNumber, @ItemName, @Category, @Description, @SerialNumber, @AcquisitionDate, @AcquisitionCost, @ConditionStatus, {DateTimeNowFunction});
                         SELECT {LastInsertIdFunction};";
            }
            else
            {
                sql = $@"UPDATE Equipment SET PropertyNumber = @PropertyNumber, ItemName = @ItemName, Category = @Category,
                            Description = @Description, SerialNumber = @SerialNumber,
                            AcquisitionDate = @AcquisitionDate, AcquisitionCost = @AcquisitionCost,
                            ConditionStatus = @ConditionStatus, UpdatedAt = {DateTimeNowFunction}
                         WHERE EquipmentId = @EquipmentId;
                         SELECT @EquipmentId;";
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
        {
            return await GetEquipmentByQRAsync(propertyNumber);
        }

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

        public async Task<int> UpdateRequestStatusAsync(string qrCode, string status, string? approver = null)
        {
            using var connection = CreateConnection();
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;
            return await connection.ExecuteAsync(
                "UPDATE Equipment SET RequestStatus = @Status WHERE EquipmentId = @EquipmentId AND RequestStatus = 'Pending'",
                new { Status = status, EquipmentId = equipment.EquipmentId });
        }

        public async Task<int> ApproveRequestAsync(string qrCode, UserModel approver)
        {
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;

            var user = await GetUserByUsernameAsync(equipment.RequestedByUsername!);
            if (user == null) return 0;

            using var connection = CreateConnection();

            await connection.ExecuteAsync(
                "UPDATE Equipment SET RequestStatus = 'Approved' WHERE EquipmentId = @EquipmentId",
                new { EquipmentId = equipment.EquipmentId });

            await connection.ExecuteAsync(
                @"INSERT INTO Assignments (EquipmentId, UserId, AssignedDate, AssignmentStatus)
                  VALUES (@EquipmentId, @UserId, @Date, 'Assigned')",
                new { EquipmentId = equipment.EquipmentId, UserId = user.UserId, Date = DateTime.Now.Date });

            equipment.ConditionStatus = "Issued";
            equipment.AssignedToUsername = user.Username;
            equipment.RequestStatus = "Approved";
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

            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                "UPDATE Equipment SET RequestStatus = 'Rejected' WHERE EquipmentId = @EquipmentId",
                new { EquipmentId = equipment.EquipmentId });

            var user = await GetUserByUsernameAsync(equipment.RequestedByUsername!);
            if (user != null)
            {
                await SendNotificationAsync(user.Username, "❌ Request Rejected",
                    $"Your request for '{equipment.ItemName}' has been rejected.");
            }

            equipment.RequestedByUsername = null;
            equipment.RequestStatus = null;
            equipment.LastUpdated = DateTime.Now;
            await SaveEquipmentAsync(equipment);
            return 1;
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
                         Timestamp, Action, Details as Remarks
                  FROM AuditLogs ORDER BY Timestamp DESC");
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
            equipment.LastUpdated = DateTime.Now;
            await SaveEquipmentAsync(equipment);

            if (!string.IsNullOrEmpty(equipment.DisposalRequestedBy))
            {
                await SendNotificationAsync(equipment.DisposalRequestedBy, "✅ Disposal Approved",
                    $"Disposal of '{equipment.ItemName}' has been approved by {approvedBy}.");
            }
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

            if (!string.IsNullOrEmpty(equipment.DisposalRequestedBy))
            {
                await SendNotificationAsync(equipment.DisposalRequestedBy, "❌ Disposal Rejected",
                    $"Disposal of '{equipment.ItemName}' was rejected by {rejectedBy}.");
            }
            return true;
        }

        // ============================================================
        // HANDSHAKES
        // ============================================================
        public async Task<int> CreateHandshakeAsync(int equipmentId, int fromUserId, int toUserId, string notes = "")
        {
            using var connection = CreateConnection();
            string sql = $@"INSERT INTO Handshakes (EquipmentId, FromUserId, ToUserId, TransferDate, Status, Notes)
                            VALUES (@EquipmentId, @FromUserId, @ToUserId, {DateTimeNowFunction}, 'Pending', @Notes);
                            SELECT {LastInsertIdFunction};";
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                EquipmentId = equipmentId,
                FromUserId = fromUserId,
                ToUserId = toUserId,
                Notes = notes
            });
        }

        public async Task<bool> AcceptHandshakeAsync(int handshakeId)
        {
            using var connection = CreateConnection();
            int rows = await connection.ExecuteAsync(
                "UPDATE Handshakes SET Status = 'Accepted' WHERE HandshakeId = @HandshakeId AND Status = 'Pending'",
                new { HandshakeId = handshakeId });
            return rows > 0;
        }

        public async Task<List<HandshakeModel>> GetPendingHandshakesForUserAsync(int userId)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<HandshakeModel>(
                "SELECT * FROM Handshakes WHERE ToUserId = @UserId AND Status = 'Pending'",
                new { UserId = userId });
            return result.ToList();
        }

        // ============================================================
        // DAMAGE REPORTS
        // ============================================================
        public async Task<int> SaveDamageReportAsync(DamageReportModel report)
        {
            using var connection = CreateConnection();
            string sql = $@"INSERT INTO DamageReports (EquipmentId, ReportedBy, IncidentDate, DamageDescription, ReportStatus, CreatedAt)
                            VALUES (@EquipmentId, @ReportedBy, @IncidentDate, @DamageDescription, @ReportStatus, {DateTimeNowFunction});
                            SELECT {LastInsertIdFunction};";
            return await connection.ExecuteScalarAsync<int>(sql, report);
        }

        public async Task<List<DamageReportModel>> GetDamageReportsForEquipmentAsync(int equipmentId)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<DamageReportModel>(
                "SELECT * FROM DamageReports WHERE EquipmentId = @EquipmentId ORDER BY CreatedAt DESC",
                new { EquipmentId = equipmentId });
            return result.ToList();
        }

        public async Task<List<DamageReportModel>> GetAllDamageReportsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<DamageReportModel>(
                "SELECT * FROM DamageReports ORDER BY CreatedAt DESC");
            return result.ToList();
        }

        public async Task<int> UpdateDamageReportStatusAsync(int reportId, string status)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(
                "UPDATE DamageReports SET ReportStatus = @Status WHERE ReportId = @ReportId",
                new { Status = status, ReportId = reportId });
        }

        // ============================================================
        // ICS DOCUMENTS
        // ============================================================
        public async Task<int> SaveIcsDocumentAsync(IcsDocumentModel ics)
        {
            using var connection = CreateConnection();
            string sql = $@"INSERT INTO IcsDocuments (EquipmentId, IssuedTo, IcsNumber, DateIssued, DocumentPath, CreatedAt)
                            VALUES (@EquipmentId, @IssuedTo, @IcsNumber, @DateIssued, @DocumentPath, {DateTimeNowFunction});
                            SELECT {LastInsertIdFunction};";
            return await connection.ExecuteScalarAsync<int>(sql, ics);
        }

        public async Task<IcsDocumentModel?> GetIcsByNumberAsync(string icsNumber)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<IcsDocumentModel>(
                "SELECT * FROM IcsDocuments WHERE IcsNumber = @IcsNumber", new { IcsNumber = icsNumber });
        }

        public async Task<List<IcsDocumentModel>> GetIcsDocumentsForEquipmentAsync(int equipmentId)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<IcsDocumentModel>(
                "SELECT * FROM IcsDocuments WHERE EquipmentId = @EquipmentId ORDER BY CreatedAt DESC",
                new { EquipmentId = equipmentId });
            return result.ToList();
        }

        public async Task<List<IcsDocumentModel>> GetIcsDocumentsForUserAsync(int userId)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<IcsDocumentModel>(
                "SELECT * FROM IcsDocuments WHERE IssuedTo = @UserId ORDER BY CreatedAt DESC",
                new { UserId = userId });
            return result.ToList();
        }
    }
}