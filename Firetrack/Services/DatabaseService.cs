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
            // No auto‑creation – tables must exist already
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

        public async Task<List<TransactionModel>> GetTransactionsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<TransactionModel>(
                "SELECT * FROM Transactions ORDER BY Timestamp DESC");
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

        // ===== OTP / PASSWORD RESET =====
        public async Task<string> GenerateOtpAsync(string username)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(
                "DELETE FROM PasswordResetOtps WHERE Username = @Username OR Expiry < GETDATE()",
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

        // ===== REQUESTS =====
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

        // ===== AUDIT LOGS =====
        public async Task LogActionAsync(string username, string action, string? details = null)
        {
            using var connection = new SqlConnection(_connectionString);
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
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<AuditLogModel>(
                "SELECT * FROM AuditLogs ORDER BY Timestamp DESC");
            return result.ToList();
        }
    }
}