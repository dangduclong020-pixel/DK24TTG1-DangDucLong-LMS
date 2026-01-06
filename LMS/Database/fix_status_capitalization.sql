-- Script để sửa lỗi viết hoa/thường của Status trong database
-- Chuyển tất cả "active" thành "Active" để đồng nhất

USE [lms_system];
GO

-- 1. Cập nhật bảng Users
UPDATE dbo.Users
SET Status = 'Active'
WHERE Status = 'active';
GO

-- 2. Cập nhật bảng ClassStudents (nếu có trường Status)
UPDATE dbo.ClassStudents
SET Status = 'Active'
WHERE Status = 'active';
GO

-- 3. Cập nhật bảng Attendances (nếu có trường Status với giá trị active)
UPDATE dbo.Attendances
SET Status = 'Open'
WHERE LOWER(Status) = 'active';
GO

-- Kiểm tra kết quả
SELECT 'Users' AS TableName, Status, COUNT(*) AS Count
FROM dbo.Users
GROUP BY Status
UNION ALL
SELECT 'ClassStudents' AS TableName, Status, COUNT(*) AS Count
FROM dbo.ClassStudents
GROUP BY Status
ORDER BY TableName, Status;
GO

PRINT 'Đã cập nhật thành công Status từ "active" sang "Active"';
GO
