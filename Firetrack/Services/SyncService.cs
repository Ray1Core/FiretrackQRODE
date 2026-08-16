using System;
using System.Threading.Tasks;
using Dapper;
using Firetrack.Models;

#if ANDROID
using Microsoft.Data.Sqlite;
#else
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
#endif

namespace Firetrack.Services
{
    public class SyncService
    {
        private readonly string _sqliteConnectionString;
        private readonly string _sqlServerConnectionString;

        public SyncService(string sqliteConnectionString, string sqlServerConnectionString)
        {
            _sqliteConnectionString = sqliteConnectionString;
            _sqlServerConnectionString = sqlServerConnectionString;
        }

        /// <summary>
        /// Syncs data from SQLite (local) to SQL Server (central)
        /// </summary>
        public async Task<bool> SyncLocalToCentralAsync()
        {
#if ANDROID
            // On Android, we can't directly connect to SQL Server, so skip sync
            await Task.CompletedTask;  // ✅ Fix CS1998
            return false;
#else
            try
            {
                if (!await IsServerReachableAsync())
                    return false;

                using var sqliteConn = new SqliteConnection(_sqliteConnectionString);
                var localEquipment = await sqliteConn.QueryAsync<EquipmentModel>(
                    "SELECT * FROM Equipment WHERE LastUpdated >= datetime('now', '-7 days')");

                using var sqlConn = new SqlConnection(_sqlServerConnectionString);
                foreach (var eq in localEquipment)
                {
                    var sql = @"
                        MERGE INTO Equipment AS target
                        USING (SELECT @QRCode AS QRCode) AS source
                        ON target.QRCode = source.QRCode
                        WHEN MATCHED THEN
                            UPDATE SET 
                                Name = @Name,
                                Type = @Type,
                                Status = @Status,
                                AssignedToUsername = @AssignedToUsername,
                                PhotoPath = @PhotoPath,
                                Remarks = @Remarks,
                                LastUpdated = @LastUpdated,
                                RequestedByUsername = @RequestedByUsername,
                                RequestStatus = @RequestStatus,
                                IsDisposalRequested = @IsDisposalRequested,
                                DisposalStatus = @DisposalStatus,
                                DisposalReason = @DisposalReason,
                                DisposalRequestedBy = @DisposalRequestedBy,
                                DisposalRequestDate = @DisposalRequestDate,
                                DisposalApprovedBy = @DisposalApprovedBy,
                                DisposalApprovalDate = @DisposalApprovalDate,
                                DisposalRemarks = @DisposalRemarks
                        WHEN NOT MATCHED THEN
                            INSERT (QRCode, Name, Type, Status, AssignedToUsername, PhotoPath, Remarks, 
                                    LastUpdated, RequestedByUsername, RequestStatus,
                                    IsDisposalRequested, DisposalStatus, DisposalReason, 
                                    DisposalRequestedBy, DisposalRequestDate, DisposalApprovedBy, 
                                    DisposalApprovalDate, DisposalRemarks)
                            VALUES (@QRCode, @Name, @Type, @Status, @AssignedToUsername, @PhotoPath, @Remarks,
                                    @LastUpdated, @RequestedByUsername, @RequestStatus,
                                    @IsDisposalRequested, @DisposalStatus, @DisposalReason,
                                    @DisposalRequestedBy, @DisposalRequestDate, @DisposalApprovedBy,
                                    @DisposalApprovalDate, @DisposalRemarks);";

                    await sqlConn.ExecuteAsync(sql, eq);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Sync failed: {ex.Message}");
                return false;
            }
#endif
        }

        /// <summary>
        /// Syncs data from SQL Server (central) to SQLite (local)
        /// </summary>
        public async Task<bool> SyncCentralToLocalAsync()
        {
#if ANDROID
            // On Android, we can't directly connect to SQL Server, so skip sync
            await Task.CompletedTask;  // ✅ Fix CS1998
            return false;
#else
            try
            {
                if (!await IsServerReachableAsync())
                    return false;

                using var sqlConn = new SqlConnection(_sqlServerConnectionString);
                var centralEquipment = await sqlConn.QueryAsync<EquipmentModel>("SELECT * FROM Equipment");

                using var sqliteConn = new SqliteConnection(_sqliteConnectionString);
                foreach (var eq in centralEquipment)
                {
                    var sql = @"
                        INSERT OR REPLACE INTO Equipment (
                            EquipmentId, QRCode, Name, Type, Status, AssignedToUsername, PhotoPath, Remarks,
                            LastUpdated, RequestedByUsername, RequestStatus,
                            IsDisposalRequested, DisposalStatus, DisposalReason, DisposalRequestedBy,
                            DisposalRequestDate, DisposalApprovedBy, DisposalApprovalDate, DisposalRemarks
                        ) VALUES (
                            @EquipmentId, @QRCode, @Name, @Type, @Status, @AssignedToUsername, @PhotoPath, @Remarks,
                            @LastUpdated, @RequestedByUsername, @RequestStatus,
                            @IsDisposalRequested, @DisposalStatus, @DisposalReason, @DisposalRequestedBy,
                            @DisposalRequestDate, @DisposalApprovedBy, @DisposalApprovalDate, @DisposalRemarks
                        );";

                    await sqliteConn.ExecuteAsync(sql, eq);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Sync failed: {ex.Message}");
                return false;
            }
#endif
        }

#if !ANDROID
        private async Task<bool> IsServerReachableAsync()
        {
            try
            {
                using var conn = new SqlConnection(_sqlServerConnectionString);
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
#endif
    }
}