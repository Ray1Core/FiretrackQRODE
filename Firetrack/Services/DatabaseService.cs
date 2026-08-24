using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Firetrack.Models;

#if ANDROID
using Microsoft.Data.Sqlite;
using DbConnection = Microsoft.Data.Sqlite.SqliteConnection;
#else
using Microsoft.Data.SqlClient;
using DbConnection = Microsoft.Data.SqlClient.SqlConnection;
#endif

namespace Firetrack.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
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

        private void InitializeDatabase()
        {
#if ANDROID
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());

            var dbPath = _connectionString.Replace("Data Source=", "");
            var directory = System.IO.Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            CreateTables(connection);
            SeedData(connection);
#else
            EnsureDatabaseExists();
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            CreateTables(connection);
            SeedData(connection);
#endif
        }

#if !ANDROID
        private void EnsureDatabaseExists()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            };
            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            int dbExists = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sys.databases WHERE name = 'FiretrackDB'");
            if (dbExists == 0)
                connection.Execute("CREATE DATABASE FiretrackDB");
        }
#endif

        private void CreateTables(IDbConnection connection)
        {
            // ---- AuditLogs (needed for LogActionAsync) ----
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL,
                    Action TEXT NOT NULL,
                    Details TEXT NULL,
                    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                )");

            // ---- PasswordResetOtps (needed for OTP) ----
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS PasswordResetOtps (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL,
                    OtpCode TEXT NOT NULL,
                    Expiry DATETIME NOT NULL,
                    IsUsed INTEGER NOT NULL DEFAULT 0
                )");

            // ---- Roles ----
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Roles (
                    RoleId INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoleName TEXT NOT NULL UNIQUE,
                    Description TEXT,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // ---- Users ----
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Users (
                    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoleId INTEGER NOT NULL,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    Email TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Status TEXT CHECK(Status IN ('Active', 'Inactive', 'Suspended')) DEFAULT 'Active',
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
                )");

            // ---- Equipment ----
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
                    ConditionStatus TEXT CHECK(ConditionStatus IN ('Serviceable', 'Unserviceable', 'Under Repair', 'Disposed')) DEFAULT 'Serviceable',
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // ---- Requests ----
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

            // ---- Assignments ----
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

            // ---- Handshakes ----
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

            // ---- DamageReports ----
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

            // ---- DisposalRequests ----
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

            // ---- Notifications ----
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Notifications (
                    NotificationId INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    IsRead BOOLEAN DEFAULT 0,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users(UserId)
                )");

            // ---- IcsDocuments ----
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
        }

        // ============================================================
        // SEED DATA (fixed SQL quotes)
        // ============================================================
        private void SeedData(IDbConnection connection)
        {
            // ---- Roles ----
            int roleCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Roles");
            if (roleCount == 0)
            {
                connection.Execute(@"
                    INSERT INTO Roles (RoleName, Description) VALUES
                    ('Admin', 'System administrator with full access'),
                    ('Personnel', 'Firefighter / regular user')");
            }

            // ---- Users ----
            int userCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Users");
            if (userCount == 0)
            {
                var adminRoleId = connection.ExecuteScalar<int>("SELECT RoleId FROM Roles WHERE RoleName = 'Admin'");
                var personnelRoleId = connection.ExecuteScalar<int>("SELECT RoleId FROM Roles WHERE RoleName = 'Personnel'");

                connection.Execute(@"
                    INSERT INTO Users (RoleId, FirstName, LastName, Email, PasswordHash, Status) VALUES
                    (@AdminRole, 'Admin', 'Chief', 'admin@firetrack.gov', 'admin123', 'Active'),
                    (@PersonnelRole, 'John', 'Firefighter', 'john@firetrack.gov', 'user123', 'Active')",
                    new { AdminRole = adminRoleId, PersonnelRole = personnelRoleId });
            }

            // ---- Equipment (FIXED: removed problematic quotes) ----
            int eqCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Equipment");
            if (eqCount == 0)
            {
                connection.Execute(@"
                    INSERT INTO Equipment (PropertyNumber, ItemName, Category, Description, SerialNumber, AcquisitionDate, AcquisitionCost, ConditionStatus) VALUES
                    ('HOSE001', 'Fire Hose 1.5 x 15m', 'Hose', 'Standard fire hose', NULL, '2023-01-01', 150.00, 'Serviceable'),
                    ('HOSE002', 'Fire Hose 2.5 x 15m', 'Hose', 'Heavy duty hose', NULL, '2023-01-15', 200.00, 'Serviceable'),
                    ('HOSE003', 'Fire Hose 2.5 x 30m', 'Hose', 'Long length hose', NULL, '2023-02-01', 300.00, 'Serviceable'),
                    ('NOZZLE001', 'Combination Nozzle', 'Nozzle', 'Multi-purpose nozzle', NULL, '2023-03-01', 80.00, 'Serviceable'),
                    ('NOZZLE002', 'Fog Nozzle', 'Nozzle', 'Fog pattern nozzle', NULL, '2023-03-15', 75.00, 'Serviceable'),
                    ('TOOL001', 'Halligan Tool', 'Rescue Tool', 'Multipurpose forcible entry tool', NULL, '2023-04-01', 120.00, 'Serviceable'),
                    ('TOOL002', 'Flathead Axe', 'Rescue Tool', 'Fire axe', NULL, '2023-04-15', 90.00, 'Serviceable'),
                    ('TOOL003', 'Pry Bar', 'Rescue Tool', 'Pry bar for rescue', NULL, '2023-05-01', 60.00, 'Serviceable'),
                    ('TOOL004', 'Bolt Cutter', 'Rescue Tool', 'Heavy duty bolt cutter', NULL, '2023-05-15', 110.00, 'Serviceable'),
                    ('TOOL005', 'Search & Rescue Rope', 'Rescue Tool', 'Rope for search and rescue', NULL, '2023-06-01', 50.00, 'Serviceable')");
            }

            // ---- Assignments (sample) ----
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
        // AUDIT LOG METHODS (FIXED - added back)
        // ============================================================
        public async Task LogActionAsync(string username, string action, string? details = null)
        {
            using var connection = CreateConnection();
            string sql = @"INSERT INTO AuditLogs (Username, Action, Details, Timestamp)
                           VALUES (@Username, @Action, @Details, @Timestamp)";
            await connection.ExecuteAsync(sql, new
            {
                Username = username,
                Action = action,
                Details = details,
                Timestamp = DateTime.Now
            });
        }

        public async Task<List<AuditLogModel>> GetAuditLogsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<AuditLogModel>(
                "SELECT * FROM AuditLogs ORDER BY Timestamp DESC");
            return result.ToList();
        }

        // ============================================================
        // OTP METHODS
        // ============================================================
        public async Task<string> GenerateOtpAsync(string username)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                "DELETE FROM PasswordResetOtps WHERE Username = @Username OR Expiry < DATETIME('now')",
                new { Username = username });

            var random = new Random();
            string otpCode = random.Next(100000, 999999).ToString();
            var expiry = DateTime.Now.AddMinutes(10);

            await connection.ExecuteAsync(
                @"INSERT INTO PasswordResetOtps (Username, OtpCode, Expiry, IsUsed)
                  VALUES (@Username, @OtpCode, @Expiry, 0)",
                new { Username = username, OtpCode = otpCode, Expiry = expiry });

            return otpCode;
        }

        public async Task<bool> ValidateOtpAsync(string username, string otpCode)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<OtpModel>(
                @"SELECT * FROM PasswordResetOtps 
                  WHERE Username = @Username AND OtpCode = @OtpCode AND IsUsed = 0 AND Expiry > DATETIME('now')",
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
        // USER METHODS
        // ============================================================
        public async Task<UserModel?> GetUserByUsernameAsync(string username)
        {
            using var connection = CreateConnection();

            // ===== ADD FALLBACK SEED CHECK =====
            try
            {
                var userCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users");
                if (userCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Users table empty – running seed...");
                    SeedData(connection);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Seed check failed: {ex.Message}");
            }

            var user = await connection.QueryFirstOrDefaultAsync<UserModel>(
                "SELECT * FROM Users WHERE Email = @Username",
                new { Username = username });

            if (user != null)
            {
                var role = await connection.QueryFirstOrDefaultAsync(
                    "SELECT RoleName FROM Roles WHERE RoleId = @RoleId",
                    new { user.RoleId });
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

            // ---- Map Role string to RoleId if RoleId is 0 ----
            if (user.RoleId == 0 && !string.IsNullOrEmpty(user.Role))
            {
                var roleId = await connection.ExecuteScalarAsync<int>(
                    "SELECT RoleId FROM Roles WHERE RoleName = @RoleName",
                    new { RoleName = user.Role });
                user.RoleId = roleId > 0 ? roleId : 2; // default to Personnel (2)
            }

            string sql = @"
        INSERT OR REPLACE INTO Users (UserId, RoleId, FirstName, LastName, Email, PasswordHash, Status, UpdatedAt)
        VALUES (@UserId, @RoleId, @FirstName, @LastName, @Email, @PasswordHash, @Status, CURRENT_TIMESTAMP);
        SELECT last_insert_rowid();";

            return await connection.ExecuteScalarAsync<int>(sql, user);
        }

        public async Task<List<UserModel>> GetUsersAsync()
        {
            using var connection = CreateConnection();
            var users = await connection.QueryAsync<UserModel>("SELECT * FROM Users");
            var result = users.ToList();

            // Load roles for each user
            foreach (var user in result)
            {
                var role = await connection.QueryFirstOrDefaultAsync(
                    "SELECT RoleName FROM Roles WHERE RoleId = @RoleId",
                    new { user.RoleId });
                user.Role = role?.RoleName ?? "Personnel";
            }
            return result;
        }

        public async Task<int> UpdateUserAsync(UserModel user)
        {
            using var connection = CreateConnection();
            string sql = @"
                UPDATE Users 
                SET RoleId = @RoleId, FirstName = @FirstName, LastName = @LastName, 
                    Email = @Email, PasswordHash = @PasswordHash, Status = @Status,
                    UpdatedAt = CURRENT_TIMESTAMP
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
        // NOTIFICATION METHODS (FIXED - uses Email as string)
        // ============================================================
        public async Task<int> SaveNotificationAsync(NotificationModel notification)
        {
            using var connection = CreateConnection();
            // Get UserId from Email
            var user = await GetUserByUsernameAsync(notification.Username);
            if (user == null) return 0;

            string sql = @"INSERT INTO Notifications (UserId, Title, Message, IsRead, CreatedAt)
                            VALUES (@UserId, @Title, @Message, @IsRead, @Timestamp);
                            SELECT last_insert_rowid();";
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                UserId = user.UserId,
                notification.Title,
                notification.Message,
                notification.IsRead,
                Timestamp = DateTime.Now
            });
        }

        public async Task<List<NotificationModel>> GetNotificationsForUserAsync(string username)
        {
            using var connection = CreateConnection();
            var user = await GetUserByUsernameAsync(username);
            if (user == null) return new List<NotificationModel>();

            var result = await connection.QueryAsync<NotificationModel>(
                @"SELECT 
                    NotificationId, 
                    UserId,
                    Title, 
                    Message, 
                    IsRead, 
                    CreatedAt as Timestamp
                  FROM Notifications 
                  WHERE UserId = @UserId 
                  ORDER BY CreatedAt DESC",
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
        // EQUIPMENT METHODS
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

            var sql = @"
                SELECT e.* 
                FROM Equipment e
                JOIN Assignments a ON e.EquipmentId = a.EquipmentId
                WHERE a.UserId = @UserId AND a.AssignmentStatus = 'Assigned'";
            var result = await connection.QueryAsync<EquipmentModel>(sql, new { UserId = user.UserId });
            return result.ToList();
        }

        public async Task<int> SaveEquipmentAsync(EquipmentModel equipment)
        {
            using var connection = CreateConnection();
            string sql = @"
                INSERT OR REPLACE INTO Equipment (
                    EquipmentId, PropertyNumber, ItemName, Category, Description,
                    SerialNumber, AcquisitionDate, AcquisitionCost, ConditionStatus,
                    UpdatedAt
                ) VALUES (
                    @EquipmentId, @PropertyNumber, @ItemName, @Category, @Description,
                    @SerialNumber, @AcquisitionDate, @AcquisitionCost, @ConditionStatus,
                    CURRENT_TIMESTAMP
                );
                SELECT last_insert_rowid();";
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
                "SELECT * FROM Equipment WHERE PropertyNumber = @QRCode",
                new { QRCode = qrCode });
        }

        public async Task<EquipmentModel?> GetEquipmentByPropertyNumberAsync(string propertyNumber)
        {
            return await GetEquipmentByQRAsync(propertyNumber);
        }

        // ============================================================
        // REQUEST METHODS
        // ============================================================
        public async Task<List<EquipmentModel>> GetPendingRequestsAsync()
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT e.* 
                FROM Equipment e
                JOIN Requests r ON e.EquipmentId = r.EquipmentId
                WHERE r.RequestStatus = 'Pending'";
            var result = await connection.QueryAsync<EquipmentModel>(sql);
            return result.ToList();
        }

        public async Task<int> UpdateRequestStatusAsync(string qrCode, string status, string? approver = null)
        {
            using var connection = CreateConnection();
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;

            return await connection.ExecuteAsync(
                @"UPDATE Requests SET RequestStatus = @Status 
                  WHERE EquipmentId = @EquipmentId AND RequestStatus = 'Pending'",
                new { Status = status, EquipmentId = equipment.EquipmentId });
        }

        public async Task<int> ApproveRequestAsync(string qrCode, UserModel approver)
        {
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;

            var user = await GetUserByUsernameAsync(equipment.RequestedByUsername!);
            if (user == null) return 0;

            using var connection = CreateConnection();

            // Update request status
            await connection.ExecuteAsync(
                @"UPDATE Requests SET RequestStatus = 'Approved' 
                  WHERE EquipmentId = @EquipmentId",
                new { EquipmentId = equipment.EquipmentId });

            // Create assignment
            await connection.ExecuteAsync(@"
                INSERT INTO Assignments (EquipmentId, UserId, AssignedDate, AssignmentStatus)
                VALUES (@EquipmentId, @UserId, @Date, 'Assigned')",
                new { EquipmentId = equipment.EquipmentId, UserId = user.UserId, Date = DateTime.Now.Date });

            // Update equipment status
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
                @"UPDATE Requests SET RequestStatus = 'Rejected' 
                  WHERE EquipmentId = @EquipmentId",
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
        // TRANSACTION METHODS (keep for compatibility)
        // ============================================================
        public async Task<int> SaveTransactionAsync(TransactionModel transaction)
        {
            using var connection = CreateConnection();
            // Use AuditLogs as transaction log for now
            await LogActionAsync(transaction.FromUser, transaction.Action,
                $"Equipment {transaction.EquipmentQR}: {transaction.FromUser} -> {transaction.ToUser}. Remarks: {transaction.Remarks}");
            return 1;
        }

        public async Task<List<TransactionModel>> GetTransactionsForEquipmentAsync(string qrCode)
        {
            using var connection = CreateConnection();
            // Query AuditLogs for equipment transactions
            var result = await connection.QueryAsync<TransactionModel>(
                @"SELECT 
                    'TransactionId' as TransactionId,
                    @QRCode as EquipmentQR,
                    Username as FromUser,
                    'System' as ToUser,
                    Timestamp,
                    Action,
                    Details as Remarks
                  FROM AuditLogs 
                  WHERE Details LIKE @Pattern
                  ORDER BY Timestamp DESC",
                new { QRCode = qrCode, Pattern = $"%{qrCode}%" });
            return result.ToList();
        }

        public async Task<List<TransactionModel>> GetTransactionsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<TransactionModel>(
                @"SELECT 
                    'TransactionId' as TransactionId,
                    'N/A' as EquipmentQR,
                    Username as FromUser,
                    'System' as ToUser,
                    Timestamp,
                    Action,
                    Details as Remarks
                  FROM AuditLogs 
                  ORDER BY Timestamp DESC");
            return result.ToList();
        }

        // ============================================================
        // DISPOSAL METHODS
        // ============================================================
        public async Task<List<EquipmentModel>> GetDisposalRequestsAsync(string? status = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT e.*, dr.* 
                FROM Equipment e
                JOIN DisposalRequests dr ON e.EquipmentId = dr.EquipmentId
                WHERE dr.DisposalStatus = @Status OR @Status IS NULL
                ORDER BY dr.CreatedAt DESC";
            var result = await connection.QueryAsync<EquipmentModel>(sql, new { Status = status ?? "Pending Review" });
            return result.ToList();
        }

        public async Task<bool> RequestDisposalAsync(string qrCode, string requestedBy, string reason)
        {
            using var connection = CreateConnection();
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return false;

            var user = await GetUserByUsernameAsync(requestedBy);
            if (user == null) return false;

            await connection.ExecuteAsync(@"
                INSERT INTO DisposalRequests (EquipmentId, RequestedBy, Reason, DisposalStatus)
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

            await connection.ExecuteAsync(@"
                UPDATE DisposalRequests 
                SET DisposalStatus = 'Approved' 
                WHERE EquipmentId = @EquipmentId AND DisposalStatus = 'Pending Review'",
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

            await connection.ExecuteAsync(@"
                UPDATE DisposalRequests 
                SET DisposalStatus = 'Rejected' 
                WHERE EquipmentId = @EquipmentId AND DisposalStatus = 'Pending Review'",
                new { EquipmentId = equipment.EquipmentId });

            if (!string.IsNullOrEmpty(equipment.DisposalRequestedBy))
            {
                await SendNotificationAsync(equipment.DisposalRequestedBy, "❌ Disposal Rejected",
                    $"Disposal of '{equipment.ItemName}' was rejected by {rejectedBy}.");
            }

            return true;
        }

        // ============================================================
        // HAND SHAKE METHODS
        // ============================================================
        public async Task<int> CreateHandshakeAsync(int equipmentId, int fromUserId, int toUserId, string notes = "")
        {
            using var connection = CreateConnection();
            string sql = @"
                INSERT INTO Handshakes (EquipmentId, FromUserId, ToUserId, TransferDate, Status, Notes)
                VALUES (@EquipmentId, @FromUserId, @ToUserId, CURRENT_TIMESTAMP, 'Pending', @Notes);
                SELECT last_insert_rowid();";
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
        // HELPER
        // ============================================================
        private IDbConnection CreateConnection()
        {
#if ANDROID
            return new SqliteConnection(_connectionString);
#else
            return new SqlConnection(_connectionString);
#endif
        }
    }
}