USE FiretrackDB;
SELECT * FROM Users;   -- should show admin/user
SELECT * FROM Equipment; -- should show 10 items
SELECT * FROM Transactions; -- should show 6 rows (for chart)







-- ============================================================
-- DATABASE: FiretrackDB
-- Complete schema with all tables, constraints, indexes, and seed data
-- ============================================================

-- Drop the database if it exists (to avoid file conflicts)
IF EXISTS (SELECT * FROM sys.databases WHERE name = 'FiretrackDB')
BEGIN
    ALTER DATABASE FiretrackDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE FiretrackDB;
END
GO

CREATE DATABASE FiretrackDB;
GO

USE FiretrackDB;
GO

-- ===== CREATE TABLES =====

CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) UNIQUE NOT NULL,
    Password NVARCHAR(100) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Role NVARCHAR(20) NOT NULL DEFAULT 'Personnel',
    IsActive BIT NOT NULL DEFAULT 1
);
GO

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
);
GO

CREATE TABLE Transactions (
    TransactionId INT IDENTITY(1,1) PRIMARY KEY,
    EquipmentQR NVARCHAR(50) NOT NULL,
    FromUser NVARCHAR(50) NOT NULL,
    ToUser NVARCHAR(50) NOT NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    Action NVARCHAR(50) NOT NULL,
    Remarks NVARCHAR(500) NULL
);
GO

CREATE TABLE Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL,
    Title NVARCHAR(100) NOT NULL,
    Message NVARCHAR(500) NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE PasswordResetOtps (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL,
    OtpCode NVARCHAR(6) NOT NULL,
    Expiry DATETIME NOT NULL,
    IsUsed BIT NOT NULL DEFAULT 0
);
GO

CREATE TABLE AuditLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL,
    Action NVARCHAR(100) NOT NULL,
    Details NVARCHAR(500) NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- ===== ADD FOREIGN KEYS =====

ALTER TABLE PasswordResetOtps
ADD CONSTRAINT FK_PasswordResetOtps_Users FOREIGN KEY (Username) REFERENCES Users(Username) ON DELETE CASCADE;

ALTER TABLE Equipment
ADD CONSTRAINT FK_Equipment_AssignedToUser FOREIGN KEY (AssignedToUsername) REFERENCES Users(Username);

ALTER TABLE Equipment
ADD CONSTRAINT FK_Equipment_RequestedByUser FOREIGN KEY (RequestedByUsername) REFERENCES Users(Username);

ALTER TABLE Transactions
ADD CONSTRAINT FK_Transactions_FromUser FOREIGN KEY (FromUser) REFERENCES Users(Username);

ALTER TABLE Transactions
ADD CONSTRAINT FK_Transactions_ToUser FOREIGN KEY (ToUser) REFERENCES Users(Username);

ALTER TABLE Notifications
ADD CONSTRAINT FK_Notifications_User FOREIGN KEY (Username) REFERENCES Users(Username);

ALTER TABLE AuditLogs
ADD CONSTRAINT FK_AuditLogs_User FOREIGN KEY (Username) REFERENCES Users(Username);
GO

-- ===== ADD INDEXES =====

CREATE INDEX IX_PasswordResetOtps_Username ON PasswordResetOtps(Username);
CREATE INDEX IX_Equipment_AssignedToUsername ON Equipment(AssignedToUsername);
CREATE INDEX IX_Equipment_RequestStatus ON Equipment(RequestStatus);
CREATE INDEX IX_Transactions_EquipmentQR ON Transactions(EquipmentQR);
CREATE INDEX IX_Notifications_Username ON Notifications(Username);
CREATE INDEX IX_AuditLogs_Username ON AuditLogs(Username);
CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp DESC);
CREATE INDEX IX_Equipment_Name ON Equipment(Name);
CREATE INDEX IX_Equipment_Type ON Equipment(Type);
CREATE INDEX IX_Transactions_Timestamp ON Transactions(Timestamp);
GO

-- ===== SEED USERS =====

INSERT INTO Users (Username, Password, FullName, Role, IsActive)
VALUES 
    ('admin', 'admin123', 'Admin Chief', 'Admin', 1),
    ('user', 'user123', 'John Firefighter', 'Personnel', 1);
GO

-- ===== SEED EQUIPMENT =====

INSERT INTO Equipment (QRCode, Name, Type, Status, AssignedToUsername, LastUpdated)
VALUES
    ('HOSE001', 'Fire Hose 1.5" x 15m', 'Hose', 'Available', NULL, GETDATE()),
    ('HOSE002', 'Fire Hose 2.5" x 15m', 'Hose', 'Available', NULL, GETDATE()),
    ('HOSE003', 'Fire Hose 2.5" x 30m', 'Hose', 'Issued', 'user', GETDATE()),
    ('NOZZLE001', 'Combination Nozzle', 'Nozzle', 'Available', NULL, GETDATE()),
    ('NOZZLE002', 'Fog Nozzle', 'Nozzle', 'Available', NULL, GETDATE()),
    ('TOOL001', 'Halligan Tool', 'Rescue Tool', 'Available', NULL, GETDATE()),
    ('TOOL002', 'Flathead Axe', 'Rescue Tool', 'Available', NULL, GETDATE()),
    ('TOOL003', 'Pry Bar', 'Rescue Tool', 'Issued', 'user', GETDATE()),
    ('TOOL004', 'Bolt Cutter', 'Rescue Tool', 'Available', NULL, GETDATE()),
    ('TOOL005', 'Search & Rescue Rope', 'Rescue Tool', 'Available', NULL, GETDATE());
GO

-- ===== SEED TRANSACTIONS (for dashboard chart) =====

DECLARE @now DATETIME = GETDATE();
INSERT INTO Transactions (EquipmentQR, FromUser, ToUser, Timestamp, Action, Remarks)
VALUES
    ('HOSE001', 'admin', 'user', DATEADD(DAY, -6, @now), 'Issue', NULL),
    ('HOSE002', 'admin', 'user', DATEADD(DAY, -5, @now), 'Issue', NULL),
    ('HOSE003', 'admin', 'user', DATEADD(DAY, -4, @now), 'Issue', NULL),
    ('NOZZLE001', 'admin', 'user', DATEADD(DAY, -3, @now), 'Issue', NULL),
    ('NOZZLE002', 'admin', 'user', DATEADD(DAY, -2, @now), 'Issue', NULL),
    ('TOOL001', 'admin', 'user', DATEADD(DAY, -1, @now), 'Issue', NULL);
GO

-- ===== VERIFICATION =====

SELECT 'Users', COUNT(*) FROM Users
UNION ALL
SELECT 'Equipment', COUNT(*) FROM Equipment
UNION ALL
SELECT 'Transactions', COUNT(*) FROM Transactions
UNION ALL
SELECT 'Notifications', COUNT(*) FROM Notifications
UNION ALL
SELECT 'PasswordResetOtps', COUNT(*) FROM PasswordResetOtps
UNION ALL
SELECT 'AuditLogs', COUNT(*) FROM AuditLogs;
GO

PRINT '✅ FiretrackDB created and seeded successfully.';
