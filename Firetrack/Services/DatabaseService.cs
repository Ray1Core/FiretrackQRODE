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
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
#if ANDROID
            // SQLite – ensure database file exists and tables are created
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            CreateTables(connection);
            SeedData(connection);
#else
            // SQL Server – use existing logic with EnsureDatabaseExists
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
            // Users
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Users (
                    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT UNIQUE NOT NULL,
                    Password TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    Role TEXT NOT NULL DEFAULT 'Personnel',
                    IsActive INTEGER NOT NULL DEFAULT 1
                )");

            // Equipment
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Equipment (
                    EquipmentId INTEGER PRIMARY KEY AUTOINCREMENT,
                    QRCode TEXT UNIQUE NOT NULL,
                    Name TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'Available',
                    AssignedToUsername TEXT NULL,
                    PhotoPath TEXT NULL,
                    Remarks TEXT NULL,
                    LastUpdated DATETIME NULL,
                    RequestedByUsername TEXT NULL,
                    RequestStatus TEXT NULL
                )");

            // Transactions
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Transactions (
                    TransactionId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EquipmentQR TEXT NOT NULL,
                    FromUser TEXT NOT NULL,
                    ToUser TEXT NOT NULL,
                    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    Action TEXT NOT NULL,
                    Remarks TEXT NULL
                )");

            // Notifications
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Notifications (
                    NotificationId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    IsRead INTEGER NOT NULL DEFAULT 0,
                    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                )");

            // PasswordResetOtps
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS PasswordResetOtps (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL,
                    OtpCode TEXT NOT NULL,
                    Expiry DATETIME NOT NULL,
                    IsUsed INTEGER NOT NULL DEFAULT 0
                )");

            // AuditLogs
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL,
                    Action TEXT NOT NULL,
                    Details TEXT NULL,
                    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                )");
        }

        private void SeedData(IDbConnection connection)
        {
            // Users
            if (!connection.QueryFirstOrDefault<UserModel>("SELECT 1 FROM Users LIMIT 1")?.Equals(null) ?? true)
            {
                connection.Execute(
                    "INSERT INTO Users (Username, Password, FullName, Role, IsActive) VALUES (@Username, @Password, @FullName, @Role, @IsActive)",
                    new[]
                    {
                        new { Username = "admin", Password = "admin123", FullName = "Admin Chief", Role = "Admin", IsActive = 1 },
                        new { Username = "user", Password = "user123", FullName = "John Firefighter", Role = "Personnel", IsActive = 1 }
                    });
            }

            // Equipment
            if (!connection.QueryFirstOrDefault<EquipmentModel>("SELECT 1 FROM Equipment LIMIT 1")?.Equals(null) ?? true)
            {
                var items = new List<EquipmentModel>
                {
                    new EquipmentModel { QRCode = "HOSE001", Name = "Fire Hose 1.5\" x 15m", Type = "Hose", Status = "Available" },
                    new EquipmentModel { QRCode = "HOSE002", Name = "Fire Hose 2.5\" x 15m", Type = "Hose", Status = "Available" },
                    new EquipmentModel { QRCode = "HOSE003", Name = "Fire Hose 2.5\" x 30m", Type = "Hose", Status = "Issued", AssignedToUsername = "user" },
                    new EquipmentModel { QRCode = "NOZZLE001", Name = "Combination Nozzle", Type = "Nozzle", Status = "Available" },
                    new EquipmentModel { QRCode = "NOZZLE002", Name = "Fog Nozzle", Type = "Nozzle", Status = "Available" },
                    new EquipmentModel { QRCode = "TOOL001", Name = "Halligan Tool", Type = "Rescue Tool", Status = "Available" },
                    new EquipmentModel { QRCode = "TOOL002", Name = "Flathead Axe", Type = "Rescue Tool", Status = "Available" },
                    new EquipmentModel { QRCode = "TOOL003", Name = "Pry Bar", Type = "Rescue Tool", Status = "Issued", AssignedToUsername = "user" },
                    new EquipmentModel { QRCode = "TOOL004", Name = "Bolt Cutter", Type = "Rescue Tool", Status = "Available" },
                    new EquipmentModel { QRCode = "TOOL005", Name = "Search & Rescue Rope", Type = "Rescue Tool", Status = "Available" }
                };
                foreach (var eq in items)
                {
                    eq.LastUpdated = DateTime.Now;
                    connection.Execute(
                        @"INSERT INTO Equipment (QRCode, Name, Type, Status, AssignedToUsername, LastUpdated)
                          VALUES (@QRCode, @Name, @Type, @Status, @AssignedToUsername, @LastUpdated)",
                        eq);
                }
            }

            // Transactions – seed with 365 days of random data (only if empty)
            if (!connection.QueryFirstOrDefault<TransactionModel>("SELECT 1 FROM Transactions LIMIT 1")?.Equals(null) ?? true)
            {
                var random = new Random();
                var now = DateTime.Now;
                var qrCodes = new[] { "HOSE001", "HOSE002", "HOSE003", "NOZZLE001", "NOZZLE002", "TOOL001", "TOOL002", "TOOL003", "TOOL004", "TOOL005" };
                for (int day = 0; day < 365; day++)
                {
                    int issuesToday = random.Next(0, 4);
                    for (int i = 0; i < issuesToday; i++)
                    {
                        string qr = qrCodes[random.Next(qrCodes.Length)];
                        connection.Execute(
                            @"INSERT INTO Transactions (EquipmentQR, FromUser, ToUser, Timestamp, Action)
                              VALUES (@QR, 'admin', 'user', @Date, 'Issue')",
                            new { QR = qr, Date = now.AddDays(-day) });
                    }
                }
            }
        }

        // ---- All the data methods (unchanged logic, but use DbConnection) ----

        public async Task<List<EquipmentModel>> GetEquipmentsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<EquipmentModel>("SELECT * FROM Equipment");
            return result.ToList();
        }

        public async Task<List<EquipmentModel>> GetEquipmentsAssignedToUserAsync(string username)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<EquipmentModel>(
                "SELECT * FROM Equipment WHERE AssignedToUsername = @Username",
                new { Username = username });
            return result.ToList();
        }

        public async Task<int> SaveEquipmentAsync(EquipmentModel equipment)
        {
            using var connection = CreateConnection();
            string sql = @"
                INSERT OR REPLACE INTO Equipment (EquipmentId, QRCode, Name, Type, Status, AssignedToUsername, PhotoPath, Remarks, LastUpdated, RequestedByUsername, RequestStatus)
                VALUES (@EquipmentId, @QRCode, @Name, @Type, @Status, @AssignedToUsername, @PhotoPath, @Remarks, @LastUpdated, @RequestedByUsername, @RequestStatus);
                SELECT last_insert_rowid();";
            return await connection.ExecuteScalarAsync<int>(sql, equipment);
        }

        public async Task<int> DeleteEquipmentAsync(EquipmentModel equipment)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync("DELETE FROM Equipment WHERE EquipmentId = @EquipmentId", equipment);
        }

        public async Task<int> SaveTransactionAsync(TransactionModel transaction)
        {
            using var connection = CreateConnection();
            string sql = @"INSERT INTO Transactions (EquipmentQR, FromUser, ToUser, Timestamp, Action, Remarks)
                            VALUES (@EquipmentQR, @FromUser, @ToUser, @Timestamp, @Action, @Remarks);
                            SELECT last_insert_rowid();";
            return await connection.ExecuteScalarAsync<int>(sql, transaction);
        }

        public async Task<List<TransactionModel>> GetTransactionsForEquipmentAsync(string qrCode)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<TransactionModel>(
                "SELECT * FROM Transactions WHERE EquipmentQR = @QRCode ORDER BY Timestamp DESC",
                new { QRCode = qrCode });
            return result.ToList();
        }

        public async Task<List<TransactionModel>> GetTransactionsAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<TransactionModel>(
                "SELECT * FROM Transactions ORDER BY Timestamp DESC");
            return result.ToList();
        }

        public async Task<UserModel?> GetUserByUsernameAsync(string username)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<UserModel>(
                "SELECT * FROM Users WHERE Username = @Username",
                new { Username = username });
        }

        public async Task<int> SaveUserAsync(UserModel user)
        {
            using var connection = CreateConnection();
            string sql = @"
                INSERT OR REPLACE INTO Users (UserId, Username, Password, FullName, Role, IsActive)
                VALUES (@UserId, @Username, @Password, @FullName, @Role, @IsActive);
                SELECT last_insert_rowid();";
            return await connection.ExecuteScalarAsync<int>(sql, user);
        }

        public async Task<List<UserModel>> GetUsersAsync()
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<UserModel>("SELECT * FROM Users");
            return result.ToList();
        }

        public async Task<int> UpdateUserAsync(UserModel user)
        {
            using var connection = CreateConnection();
            string sql = @"
                UPDATE Users 
                SET Username = @Username, Password = @Password, FullName = @FullName, 
                    Role = @Role, IsActive = @IsActive
                WHERE UserId = @UserId";
            return await connection.ExecuteAsync(sql, user);
        }

        public async Task<bool> ResetPasswordAsync(string username, string newPassword)
        {
            using var connection = CreateConnection();
            int rows = await connection.ExecuteAsync(
                "UPDATE Users SET Password = @Password WHERE Username = @Username",
                new { Password = newPassword, Username = username });
            return rows > 0;
        }

        // OTP methods...
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

        // Requests
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
            return await connection.ExecuteAsync(
                "UPDATE Equipment SET RequestStatus = @Status WHERE QRCode = @QRCode",
                new { Status = status, QRCode = qrCode });
        }

        public async Task<int> ApproveRequestAsync(string qrCode, UserModel approver)
        {
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;

            var user = await GetUserByUsernameAsync(equipment.RequestedByUsername!);
            if (user == null) return 0;

            equipment.AssignedToUsername = user.Username;
            equipment.Status = "Issued";
            equipment.RequestStatus = "Approved";
            equipment.LastUpdated = DateTime.Now;

            var transaction = new TransactionModel
            {
                EquipmentQR = equipment.QRCode,
                FromUser = approver.Username,
                ToUser = user.Username,
                Timestamp = DateTime.Now,
                Action = "Issue",
                Remarks = $"Approved by {approver.FullName}"
            };

            await SaveEquipmentAsync(equipment);
            await SaveTransactionAsync(transaction);
            await SendNotificationAsync(user.Username, "✅ Request Approved",
                $"Your request for '{equipment.Name}' has been approved.");
            return 1;
        }

        public async Task<int> RejectRequestAsync(string qrCode, UserModel approver)
        {
            var equipment = await GetEquipmentByQRAsync(qrCode);
            if (equipment == null) return 0;

            var user = await GetUserByUsernameAsync(equipment.RequestedByUsername!);
            if (user != null)
            {
                await SendNotificationAsync(user.Username, "❌ Request Rejected",
                    $"Your request for '{equipment.Name}' has been rejected.");
            }

            equipment.RequestedByUsername = null;
            equipment.RequestStatus = null;
            equipment.LastUpdated = DateTime.Now;
            await SaveEquipmentAsync(equipment);
            return 1;
        }

        public async Task<EquipmentModel?> GetEquipmentByQRAsync(string qrCode)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<EquipmentModel>(
                "SELECT * FROM Equipment WHERE QRCode = @QRCode",
                new { QRCode = qrCode });
        }

        // Notifications
        public async Task<int> SaveNotificationAsync(NotificationModel notification)
        {
            using var connection = CreateConnection();
            string sql = @"INSERT INTO Notifications (Username, Title, Message, IsRead, Timestamp)
                            VALUES (@Username, @Title, @Message, @IsRead, @Timestamp);
                            SELECT last_insert_rowid();";
            return await connection.ExecuteScalarAsync<int>(sql, notification);
        }

        public async Task<List<NotificationModel>> GetNotificationsForUserAsync(string username)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<NotificationModel>(
                "SELECT * FROM Notifications WHERE Username = @Username ORDER BY Timestamp DESC",
                new { Username = username });
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
            return await connection.ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE Username = @Username",
                new { Username = username });
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

        // Audit Logs
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

        // Helper to create connection based on platform
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