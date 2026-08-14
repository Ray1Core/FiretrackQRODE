-- ============================================================
-- DATABASE: FiretrackDB
-- Complete schema with all tables, constraints, indexes, and seed data
-- ============================================================

-- 1. CREATE DATABASE (if it doesn't exist)
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FiretrackDB')
BEGIN
    CREATE DATABASE FiretrackDB;
END
GO

USE FiretrackDB;
GO

-- 2. DROP TABLES (in correct dependency order)
IF OBJECT_ID('PasswordResetOtps', 'U') IS NOT NULL DROP TABLE PasswordResetOtps;
IF OBJECT_ID('AuditLogs', 'U') IS NOT NULL DROP TABLE AuditLogs;
IF OBJECT_ID('Notifications', 'U') IS NOT NULL DROP TABLE Notifications;
IF OBJECT_ID('Transactions', 'U') IS NOT NULL DROP TABLE Transactions;
IF OBJECT_ID('Equipment', 'U') IS NOT NULL DROP TABLE Equipment;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
GO

-- 3. CREATE TABLES
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

-- 4. ADD FOREIGN KEYS
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

-- 5. INDEXES
CREATE INDEX IX_PasswordResetOtps_Username ON PasswordResetOtps(Username);
CREATE INDEX IX_Equipment_AssignedToUsername ON Equipment(AssignedToUsername);
CREATE INDEX IX_Equipment_RequestStatus ON Equipment(RequestStatus);
CREATE INDEX IX_Transactions_EquipmentQR ON Transactions(EquipmentQR);
CREATE INDEX IX_Notifications_Username ON Notifications(Username);
CREATE INDEX IX_AuditLogs_Username ON AuditLogs(Username);
CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp DESC);

-- Optional: indexes for new features (if not already present)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Equipment_Name' AND object_id = OBJECT_ID('Equipment'))
    CREATE INDEX IX_Equipment_Name ON Equipment(Name);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Equipment_Type' AND object_id = OBJECT_ID('Equipment'))
    CREATE INDEX IX_Equipment_Type ON Equipment(Type);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Transactions_Timestamp' AND object_id = OBJECT_ID('Transactions'))
    CREATE INDEX IX_Transactions_Timestamp ON Transactions(Timestamp);
GO

-- 6. SEED DATA
INSERT INTO Users (Username, Password, FullName, Role, IsActive)
VALUES 
    ('admin', 'admin123', 'Admin Chief', 'Admin', 1),
    ('user', 'user123', 'John Firefighter', 'Personnel', 1);
GO

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

-- 7. VERIFICATION
-- 5. INDEXES
-- (Existing indexes – some may already exist, so we check)

USE FiretrackDB;
GO

-- Add only the indexes we need for the new features (if they don't already exist)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Equipment_Name' AND object_id = OBJECT_ID('Equipment'))
    CREATE INDEX IX_Equipment_Name ON Equipment(Name);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Equipment_Type' AND object_id = OBJECT_ID('Equipment'))
    CREATE INDEX IX_Equipment_Type ON Equipment(Type);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Transactions_Timestamp' AND object_id = OBJECT_ID('Transactions'))
    CREATE INDEX IX_Transactions_Timestamp ON Transactions(Timestamp);
GO

PRINT '✅ New indexes added successfully.';