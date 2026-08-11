using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Firetrack.Models;

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
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            // ===== CREATE TABLES IF MISSING =====

            // Users (with IsActive)
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                CREATE TABLE Users (
                    UserId INT IDENTITY(1,1) PRIMARY KEY,
                    Username NVARCHAR(50) UNIQUE NOT NULL,
                    Password NVARCHAR(100) NOT NULL,
                    FullName NVARCHAR(100) NOT NULL,
                    Role NVARCHAR(20) NOT NULL DEFAULT 'Personnel',
                    IsActive BIT NOT NULL DEFAULT 1
                )");

            // Add IsActive if missing
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'IsActive')
                BEGIN
                    ALTER TABLE Users ADD IsActive BIT NOT NULL DEFAULT 1;
                END");

            // Equipment (with request columns)
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Equipment' AND xtype='U')
                CREATE TABLE Equipment (
                    EquipmentId INT IDENTITY(1,1) PRIMARY KEY,
                    QRCode NVARCHAR(50) UNIQUE NOT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    Type NVARCHAR(50) NOT NULL,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Available',
                    AssignedToUsername NVARCHAR(50) NULL,
                    PhotoPath NVARCHAR(500) NULL,
                    Remarks NVARCHAR(500) NULL,
                    LastUpdated DATETIME NULL,
                    RequestedByUsername NVARCHAR(50) NULL,
                    RequestStatus NVARCHAR(20) NULL
                )");

            // Add RequestedByUsername if missing
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'Equipment' AND COLUMN_NAME = 'RequestedByUsername')
                BEGIN
                    ALTER TABLE Equipment ADD RequestedByUsername NVARCHAR(50) NULL;
                END");

            // Add RequestStatus if missing
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'Equipment' AND COLUMN_NAME = 'RequestStatus')
                BEGIN
                    ALTER TABLE Equipment ADD RequestStatus NVARCHAR(20) NULL;
                END");

            // PasswordResetOtps
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasswordResetOtps' AND xtype='U')
                CREATE TABLE PasswordResetOtps (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Username NVARCHAR(50) NOT NULL,
                    OtpCode NVARCHAR(6) NOT NULL,
                    Expiry DATETIME NOT NULL,
                    IsUsed BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_PasswordResetOtps_Users FOREIGN KEY (Username)
                        REFERENCES Users(Username) ON DELETE CASCADE
                )");

            // Add index if missing
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_PasswordResetOtps_Username' AND object_id = OBJECT_ID('PasswordResetOtps'))
                BEGIN
                    CREATE INDEX IX_PasswordResetOtps_Username ON PasswordResetOtps(Username);
                END");

            // Transactions
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Transactions' AND xtype='U')
                CREATE TABLE Transactions (
                    TransactionId INT IDENTITY(1,1) PRIMARY KEY,
                    EquipmentQR NVARCHAR(50) NOT NULL,
                    FromUser NVARCHAR(50) NOT NULL,
                    ToUser NVARCHAR(50) NOT NULL,
                    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
                    Action NVARCHAR(50) NOT NULL,
                    Remarks NVARCHAR(500) NULL
                )");

            // Notifications
            connection.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Notifications' AND xtype='U')
                CREATE TABLE Notifications (
                    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
                    Username NVARCHAR(50) NOT NULL,
                    Title NVARCHAR(100) NOT NULL,
                    Message NVARCHAR(500) NOT NULL,
                    IsRead BIT NOT NULL DEFAULT 0,
                    Timestamp DATETIME NOT NULL DEFAULT GETDATE()
                )");

            // ===== SEED USERS =====
            var admin = connection.QueryFirstOrDefault<UserModel>(
                "SELECT * FROM Users WHERE Username = @Username", new { Username = "admin" });
            if (admin == null)
            {
                connection.Execute(
                    "INSERT INTO Users (Username, Password, FullName, Role, IsActive) VALUES (@Username, @Password, @FullName, @Role, @IsActive)",
                    new { Username = "admin", Password = "admin123", FullName = "Admin Chief", Role = "Admin", IsActive = true });
            }

            var user = connection.QueryFirstOrDefault<UserModel>(
                "SELECT * FROM Users WHERE Username = @Username", new { Username = "user" });
            if (user == null)
            {
                connection.Execute(
                    "INSERT INTO Users (Username, Password, FullName, Role, IsActive) VALUES (@Username, @Password, @FullName, @Role, @IsActive)",
                    new { Username = "user", Password = "user123", FullName = "John Firefighter", Role = "Personnel", IsActive = true });
            }

            // ===== SEED EQUIPMENT =====
            var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Equipment");
            if (count == 0)
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
        }

        // ---------- Equipment ----------
        public async Task<List<EquipmentModel>> GetEquipmentsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<EquipmentModel>("SELECT * FROM Equipment");
            return result.ToList();
        }

        public async Task<List<EquipmentModel>> GetEquipmentsAssignedToUserAsync(string username)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<EquipmentModel>(
                "SELECT * FROM Equipment WHERE AssignedToUsername = @Username",
                new { Username = username });
            return result.ToList();
        }

        public async Task<int> SaveEquipmentAsync(EquipmentModel equipment)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                IF EXISTS (SELECT 1 FROM Equipment WHERE EquipmentId = @EquipmentId)
                    UPDATE Equipment SET QRCode = @QRCode, Name = @Name, Type = @Type, Status = @Status,
                        AssignedToUsername = @AssignedToUsername, PhotoPath = @PhotoPath, Remarks = @Remarks,
                        LastUpdated = @LastUpdated, RequestedByUsername = @RequestedByUsername,
                        RequestStatus = @RequestStatus
                    WHERE EquipmentId = @EquipmentId
                ELSE
                    INSERT INTO Equipment (QRCode, Name, Type, Status, AssignedToUsername, PhotoPath, Remarks, LastUpdated,
                        RequestedByUsername, RequestStatus)
                    VALUES (@QRCode, @Name, @Type, @Status, @AssignedToUsername, @PhotoPath, @Remarks, @LastUpdated,
                        @RequestedByUsername, @RequestStatus);
                    SELECT CAST(SCOPE_IDENTITY() as int);";
            return await connection.ExecuteScalarAsync<int>(sql, equipment);
        }

        public async Task<int> DeleteEquipmentAsync(EquipmentModel equipment)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteAsync("DELETE FROM Equipment WHERE EquipmentId = @EquipmentId", equipment);
        }

        // ---------- Transactions ----------
        public async Task<int> SaveTransactionAsync(TransactionModel transaction)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"INSERT INTO Transactions (EquipmentQR, FromUser, ToUser, Timestamp, Action, Remarks)
                            VALUES (@EquipmentQR, @FromUser, @ToUser, @Timestamp, @Action, @Remarks);
                            SELECT CAST(SCOPE_IDENTITY() as int);";
            return await connection.ExecuteScalarAsync<int>(sql, transaction);
        }

        public async Task<List<TransactionModel>> GetTransactionsForEquipmentAsync(string qrCode)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<TransactionModel>(
                "SELECT * FROM Transactions WHERE EquipmentQR = @QRCode ORDER BY Timestamp DESC",
                new { QRCode = qrCode });
            return result.ToList();
        }

        // ---------- Users ----------
        public async Task<UserModel?> GetUserByUsernameAsync(string username)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<UserModel>(
                "SELECT * FROM Users WHERE Username = @Username",
                new { Username = username });
        }

        public async Task<int> SaveUserAsync(UserModel user)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                IF EXISTS (SELECT 1 FROM Users WHERE UserId = @UserId)
                    UPDATE Users SET Username = @Username, Password = @Password, FullName = @FullName, Role = @Role, IsActive = @IsActive
                    WHERE UserId = @UserId
                ELSE
                    INSERT INTO Users (Username, Password, FullName, Role, IsActive)
                    VALUES (@Username, @Password, @FullName, @Role, @IsActive);
                    SELECT CAST(SCOPE_IDENTITY() as int);";
            return await connection.ExecuteScalarAsync<int>(sql, user);
        }

        public async Task<List<UserModel>> GetUsersAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<UserModel>("SELECT * FROM Users");
            return result.ToList();
        }

        // ===== USER MANAGEMENT METHODS =====
        public async Task<int> UpdateUserAsync(UserModel user)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                UPDATE Users 
                SET Username = @Username, Password = @Password, FullName = @FullName, 
                    Role = @Role, IsActive = @IsActive
                WHERE UserId = @UserId";
            return await connection.ExecuteAsync(sql, user);
        }

        public async Task<bool> ResetPasswordAsync(string username, string newPassword)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = "UPDATE Users SET Password = @Password WHERE Username = @Username";
            int rows = await connection.ExecuteAsync(sql, new { Password = newPassword, Username = username });
            return rows > 0;
        }

        // ===== OTP / PASSWORD RESET METHODS =====
        public async Task<string> GenerateOtpAsync(string username)
        {
            using var connection = new SqlConnection(_connectionString);

            // Delete old OTPs for this user
            await connection.ExecuteAsync(
                "DELETE FROM PasswordResetOtps WHERE Username = @Username OR Expiry < GETDATE()",
                new { Username = username });

            // Generate 6-digit OTP
            var random = new Random();
            string otpCode = random.Next(100000, 999999).ToString();

            // Insert new OTP
            var expiry = DateTime.Now.AddMinutes(10);
            await connection.ExecuteAsync(
                @"INSERT INTO PasswordResetOtps (Username, OtpCode, Expiry, IsUsed)
                  VALUES (@Username, @OtpCode, @Expiry, 0)",
                new { Username = username, OtpCode = otpCode, Expiry = expiry });

            return otpCode;
        }

        public async Task<bool> ValidateOtpAsync(string username, string otpCode)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryFirstOrDefaultAsync<OtpModel>(
                @"SELECT * FROM PasswordResetOtps 
                  WHERE Username = @Username AND OtpCode = @OtpCode AND IsUsed = 0 AND Expiry > GETDATE()",
                new { Username = username, OtpCode = otpCode });

            return result != null;
        }

        public async Task MarkOtpUsedAsync(string username, string otpCode)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(
                "UPDATE PasswordResetOtps SET IsUsed = 1 WHERE Username = @Username AND OtpCode = @OtpCode",
                new { Username = username, OtpCode = otpCode });
        }
        // ===========================

        // ===== REQUEST METHODS =====
        public async Task<List<EquipmentModel>> GetPendingRequestsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<EquipmentModel>(
                "SELECT * FROM Equipment WHERE RequestStatus = 'Pending'");
            return result.ToList();
        }

        public async Task<int> UpdateRequestStatusAsync(string qrCode, string status, string? approver = null)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = "UPDATE Equipment SET RequestStatus = @Status WHERE QRCode = @QRCode";
            return await connection.ExecuteAsync(sql, new { Status = status, QRCode = qrCode });
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
        // ==========================

        public async Task<EquipmentModel?> GetEquipmentByQRAsync(string qrCode)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<EquipmentModel>(
                "SELECT * FROM Equipment WHERE QRCode = @QRCode",
                new { QRCode = qrCode });
        }

        // ---------- Notifications ----------
        public async Task<int> SaveNotificationAsync(NotificationModel notification)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"INSERT INTO Notifications (Username, Title, Message, IsRead, Timestamp)
                            VALUES (@Username, @Title, @Message, @IsRead, @Timestamp);
                            SELECT CAST(SCOPE_IDENTITY() as int);";
            return await connection.ExecuteScalarAsync<int>(sql, notification);
        }

        public async Task<List<NotificationModel>> GetNotificationsForUserAsync(string username)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<NotificationModel>(
                "SELECT * FROM Notifications WHERE Username = @Username ORDER BY Timestamp DESC",
                new { Username = username });
            return result.ToList();
        }

        public async Task<int> MarkNotificationAsReadAsync(int notificationId)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE NotificationId = @NotificationId",
                new { NotificationId = notificationId });
        }

        public async Task<int> MarkAllNotificationsAsReadAsync(string username)
        {
            using var connection = new SqlConnection(_connectionString);
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
    }
}