-- ============================================================
-- Database: FiretrackDB
-- Purpose: Full schema creation and initial data seed
-- Author: Firetrack Team
-- ============================================================

-- Ensure we're using the correct database (create if missing)
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FiretrackDB')
BEGIN
    CREATE DATABASE FiretrackDB;
END
GO

USE FiretrackDB;
GO

-- ============================================================
-- 1. DROP existing tables (clean slate)
-- ============================================================
IF OBJECT_ID('PasswordResetOtps', 'U') IS NOT NULL DROP TABLE PasswordResetOtps;
IF OBJECT_ID('Notifications', 'U') IS NOT NULL DROP TABLE Notifications;
IF OBJECT_ID('Transactions', 'U') IS NOT NULL DROP TABLE Transactions;
IF OBJECT_ID('Equipment', 'U') IS NOT NULL DROP TABLE Equipment;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
GO

-- ============================================================
-- 2. CREATE TABLES
-- ============================================================

-- Users table
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) UNIQUE NOT NULL,
    Password NVARCHAR(100) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Role NVARCHAR(20) NOT NULL DEFAULT 'Personnel',
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- Equipment table (includes request columns)
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

-- Transactions table
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

-- Notifications table
CREATE TABLE Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL,
    Title NVARCHAR(100) NOT NULL,
    Message NVARCHAR(500) NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- PasswordResetOtps table (for OTP flow)
CREATE TABLE PasswordResetOtps (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL,
    OtpCode NVARCHAR(6) NOT NULL,
    Expiry DATETIME NOT NULL,
    IsUsed BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_PasswordResetOtps_Users FOREIGN KEY (Username)
        REFERENCES Users(Username) ON DELETE CASCADE
);
GO

-- Index for faster lookups on Username in OTP table
CREATE INDEX IX_PasswordResetOtps_Username ON PasswordResetOtps(Username);
GO

-- ============================================================
-- 3. SEED DATA (initial users and equipment)
-- ============================================================

-- Insert default admin account
INSERT INTO Users (Username, Password, FullName, Role, IsActive)
VALUES ('admin', 'admin123', 'Admin Chief', 'Admin', 1);

-- Insert default personnel account
INSERT INTO Users (Username, Password, FullName, Role, IsActive)
VALUES ('user', 'user123', 'John Firefighter', 'Personnel', 1);

-- Insert sample equipment
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

-- Verify the data
SELECT 'Users' AS TableName, COUNT(*) AS RowCount FROM Users
UNION ALL
SELECT 'Equipment', COUNT(*) FROM Equipment
UNION ALL
SELECT 'Transactions', COUNT(*) FROM Transactions
UNION ALL
SELECT 'Notifications', COUNT(*) FROM Notifications
UNION ALL
SELECT 'PasswordResetOtps', COUNT(*) FROM PasswordResetOtps;
GO

PRINT 'Database reset and seeded successfully.';