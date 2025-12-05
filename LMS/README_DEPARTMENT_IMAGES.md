# Hướng dẫn cập nhật hình ảnh cho các khoa

## Mô tả
Tính năng này cho phép thêm hình ảnh đại diện cho từng khoa trong hệ thống LMS.

## Các hình ảnh có sẵn trong thư mục `/wwwroot/images/Department/`:
- `01-KTTC.png` - Dành cho các khoa Kỹ thuật Tự Chọn/Công nghệ thông tin
- `03-KTCN.png` - Dành cho các khoa Kinh tế 
- `04-NGNG.png` - Dành cho các khoa Ngoại ngữ
- `06-KTXDGT.png` - Dành cho khoa Kỹ thuật Xây dựng
- `07-NN.png` - Dành cho khoa Nông nghiệp
- `09-CNVH-TT-DL.png` - Dành cho khoa Công nghệ Văn hóa - Thông tin - Du lịch

## Mapping mặc định:
### Khoa Công nghệ thông tin (01-KTTC.png):
- CNPM - Bộ môn Công nghệ Phần mềm
- HTTT - Bộ môn Hệ thống Thông tin  
- KTMT - Bộ môn Kỹ thuật Máy tính

### Khoa Kinh tế (03-KTCN.png):
- QTKD - Bộ môn Quản trị Kinh doanh
- KT_TC - Bộ môn Kinh tế - Tài chính

### Khoa Ngoại ngữ (04-NGNG.png):
- ANH_VAN - Bộ môn Tiếng Anh
- NHAT_NGU - Bộ môn Tiếng Nhật

### Khoa mới được thêm:
- KTXD - Khoa Kỹ thuật Xây dựng (06-KTXDGT.png)
- NONG_NGHIEP - Khoa Nông nghiệp (07-NN.png)
- CNVH_TT_DL - Khoa Công nghệ Văn hóa - Thông tin - Du lịch (09-CNVH-TT-DL.png)

## Cách sử dụng:

### 1. Thông qua Web Interface:
1. Truy cập vào `/Users/Departments`
2. Nhấp nút "Cập nhật hình ảnh khoa"
3. Hệ thống sẽ tự động:
   - Thêm cột ImagePath nếu chưa có
   - Cập nhật hình ảnh cho các khoa hiện có
   - Thêm các khoa mới với hình ảnh tương ứng

### 2. Thông qua SQL Script:
Chạy file: `Database/update_departments_with_images.sql`

## Cấu trúc Database:
- Đã thêm cột `ImagePath` (nvarchar(500)) vào bảng `Departments`
- Model `Department.cs` đã được cập nhật với thuộc tính `ImagePath`
- `LmsSystemContext.cs` đã được cấu hình cho cột mới

## CSS Styling:
- `.department-image` - Style cho hình ảnh khoa
- `.department-image-placeholder` - Style cho placeholder khi chưa có hình
- Hiệu ứng hover và transition đã được thêm vào

## Lưu ý:
- Hình ảnh được lưu với đường dẫn tương đối từ thư mục wwwroot
- Nếu khoa chưa có hình ảnh, sẽ hiển thị placeholder với icon university
- Hệ thống tự động kiểm tra và không tạo duplicate khi thêm khoa mới