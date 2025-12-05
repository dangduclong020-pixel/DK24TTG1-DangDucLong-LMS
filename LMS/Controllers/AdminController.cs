using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using OfficeOpenXml;
using System.Text;
using System.Reflection;

namespace LMS.Controllers
{
    public class AdminController : Controller
    {
        private readonly LmsSystemContext _context;

        public AdminController(LmsSystemContext context)
        {
            _context = context;
        }

        // GET: Admin Dashboard
        public async Task<IActionResult> Index()
        {
            // Lấy thống kê tổng quan
            var totalUsers = await _context.Users.CountAsync(u => u.DeletedAt == null);
            var totalStudents = await _context.Users.CountAsync(u => u.RoleId == 4 && u.DeletedAt == null);
            var totalTeachers = await _context.Users.CountAsync(u => u.RoleId == 3 && u.DeletedAt == null);
            var totalFaculties = await _context.Faculties.CountAsync(f => f.IsActive == true && f.DeletedAt == null);
            var totalDepartments = await _context.Departments.CountAsync(d => d.IsActive == true && d.DeletedAt == null);
            var totalCourses = await _context.Courses.CountAsync(c => c.IsActive == true && c.DeletedAt == null);
            var totalClasses = await _context.Classes.CountAsync(c => c.IsActive == true);

            // Thống kê theo tháng (12 tháng gần nhất)
            var monthlyStats = new List<dynamic>();
            for (int i = 11; i >= 0; i--)
            {
                var month = DateTime.Now.AddMonths(-i);
                var startDate = new DateTime(month.Year, month.Month, 1);
                var endDate = startDate.AddMonths(1);
                
                var usersInMonth = await _context.Users
                    .Where(u => u.CreatedAt >= startDate && u.CreatedAt < endDate && u.DeletedAt == null)
                    .CountAsync();
                
                monthlyStats.Add(new { Month = month.ToString("MM/yyyy"), Users = usersInMonth });
            }

            // Thống kê theo vai trò  
            var roleStats = new
            {
                Admin = await _context.Users.CountAsync(u => u.RoleId == 1 && u.DeletedAt == null),
                Manager = await _context.Users.CountAsync(u => u.RoleId == 2 && u.DeletedAt == null),
                Teacher = totalTeachers,
                Student = totalStudents
            };

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalTeachers = totalTeachers;
            ViewBag.TotalFaculties = totalFaculties;
            ViewBag.TotalDepartments = totalDepartments;
            ViewBag.TotalCourses = totalCourses;
            ViewBag.TotalClasses = totalClasses;
            ViewBag.MonthlyStats = monthlyStats;
            ViewBag.RoleStats = roleStats;

            return View();
        }

        // 1.1. QUẢN LÝ NGƯỜI DÙNG
        public async Task<IActionResult> UserManagement()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Faculty)
                .Include(u => u.Department)
                .Where(u => u.DeletedAt == null)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(users);
        }

        // GET: Tạo tài khoản mới
        public async Task<IActionResult> CreateUser()
        {
            ViewBag.Roles = await _context.Roles.Select(r => new { r.RoleId, r.RoleName }).ToListAsync();
            ViewBag.Faculties = await _context.Faculties
                .Where(f => f.IsActive == true)
                .Select(f => new { f.FacultyId, f.Name, f.Code })
                .ToListAsync();
            ViewBag.Departments = await _context.Departments
                .Where(d => d.IsActive == true)
                .Select(d => new { d.DepartmentId, d.Name, d.Code, d.FacultyId })
                .ToListAsync();
            
            return View();
        }

        // POST: Tạo tài khoản mới
        [HttpPost]
        public async Task<IActionResult> CreateUser(User user)
        {
            try
            {
                user.CreatedAt = DateTime.Now;
                user.Status = "Active";
                
                // Tạo mã tự động dựa trên role
                if (user.RoleId == 3) // Giảng viên
                {
                    var lastTeacher = await _context.Users
                        .Where(u => u.RoleId == 3)
                        .OrderByDescending(u => u.MssvMgv)
                        .FirstOrDefaultAsync();
                    
                    var nextNumber = 1;
                    if (lastTeacher != null && lastTeacher.MssvMgv.StartsWith("GV"))
                    {
                        if (int.TryParse(lastTeacher.MssvMgv.Substring(2), out int lastNumber))
                        {
                            nextNumber = lastNumber + 1;
                        }
                    }
                    user.MssvMgv = $"GV{nextNumber:D3}";
                }
                else if (user.RoleId == 2) // Quản trị khoa
                {
                    var lastManager = await _context.Users
                        .Where(u => u.RoleId == 2)
                        .OrderByDescending(u => u.MssvMgv)
                        .FirstOrDefaultAsync();
                    
                    var nextNumber = 1;
                    if (lastManager != null && lastManager.MssvMgv.StartsWith("MGR"))
                    {
                        if (int.TryParse(lastManager.MssvMgv.Substring(3), out int lastNumber))
                        {
                            nextNumber = lastNumber + 1;
                        }
                    }
                    user.MssvMgv = $"MGR{nextNumber:D3}";
                }

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Tạo tài khoản thành công! Mã số: {user.MssvMgv}";
                return RedirectToAction("UserManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo tài khoản: {ex.Message}";
                ViewBag.Roles = await _context.Roles.Select(r => new { r.RoleId, r.RoleName }).ToListAsync();
                ViewBag.Faculties = await _context.Faculties
                    .Where(f => f.IsActive == true)
                    .Select(f => new { f.FacultyId, f.Name, f.Code })
                    .ToListAsync();
                ViewBag.Departments = await _context.Departments
                    .Where(d => d.IsActive == true)
                    .Select(d => new { d.DepartmentId, d.Name, d.Code, d.FacultyId })
                    .ToListAsync();
                return View(user);
            }
        }

        // 1.2. QUẢN LÝ KHOA - BỘ MÔN
        public async Task<IActionResult> FacultyManagement()
        {
            var faculties = await _context.Faculties
                .Include(f => f.Departments.Where(d => d.DeletedAt == null))
                .Include(f => f.Users.Where(u => u.DeletedAt == null))
                .Where(f => f.DeletedAt == null)
                .OrderBy(f => f.Name)
                .ToListAsync();

            return View(faculties);
        }

        // GET: Tạo khoa mới
        public IActionResult CreateFaculty()
        {
            return View();
        }

        // POST: Tạo khoa mới
        [HttpPost]
        public async Task<IActionResult> CreateFaculty(Faculty faculty, IFormFile imageFile)
        {
            try
            {
                // Xử lý upload hình ảnh
                if (imageFile != null && imageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(imageFile.FileName).ToLower();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Chỉ chấp nhận file hình ảnh (.jpg, .jpeg, .png, .gif)";
                        return View(faculty);
                    }

                    if (imageFile.Length > 5 * 1024 * 1024) // 5MB
                    {
                        TempData["ErrorMessage"] = "Kích thước file không được vượt quá 5MB";
                        return View(faculty);
                    }

                    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "faculties");
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                    }

                    var fileName = $"faculty_{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    faculty.ImagePath = $"/images/faculties/{fileName}";
                }

                faculty.CreatedAt = DateTime.Now;
                faculty.IsActive = true;

                _context.Faculties.Add(faculty);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo khoa mới thành công!";
                return RedirectToAction("FacultyManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo khoa: {ex.Message}";
                return View(faculty);
            }
        }

        // GET: Chỉnh sửa khoa
        public async Task<IActionResult> EditFaculty(int id)
        {
            var faculty = await _context.Faculties.FindAsync(id);
            if (faculty == null || faculty.DeletedAt != null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khoa!";
                return RedirectToAction("FacultyManagement");
            }
            return View(faculty);
        }

        // POST: Chỉnh sửa khoa
        [HttpPost]
        public async Task<IActionResult> EditFaculty(Faculty faculty, IFormFile imageFile)
        {
            try
            {
                var existingFaculty = await _context.Faculties.FindAsync(faculty.FacultyId);
                if (existingFaculty == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy khoa!";
                    return View(faculty);
                }

                // Xử lý upload hình ảnh mới
                if (imageFile != null && imageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(imageFile.FileName).ToLower();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Chỉ chấp nhận file hình ảnh (.jpg, .jpeg, .png, .gif)";
                        return View(faculty);
                    }

                    if (imageFile.Length > 5 * 1024 * 1024) // 5MB
                    {
                        TempData["ErrorMessage"] = "Kích thước file không được vượt quá 5MB";
                        return View(faculty);
                    }

                    // Xóa hình ảnh cũ nếu có
                    if (!string.IsNullOrEmpty(existingFaculty.ImagePath))
                    {
                        var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingFaculty.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "faculties");
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                    }

                    var fileName = $"faculty_{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    existingFaculty.ImagePath = $"/images/faculties/{fileName}";
                }

                // Cập nhật thông tin
                existingFaculty.Name = faculty.Name;
                existingFaculty.Code = faculty.Code;
                existingFaculty.Description = faculty.Description;
                // existingFaculty.UpdatedAt = DateTime.Now; // Remove if Faculty model doesn't have UpdatedAt

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật khoa thành công!";
                return RedirectToAction("FacultyManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật khoa: {ex.Message}";
                return View(faculty);
            }
        }

        // POST: Xóa khoa
        [HttpPost]
        public async Task<IActionResult> DeleteFaculty(int id)
        {
            try
            {
                var faculty = await _context.Faculties.FindAsync(id);
                if (faculty == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khoa!" });
                }

                // Kiểm tra xem khoa có bộ môn hoặc người dùng không
                var hasDepartments = await _context.Departments.AnyAsync(d => d.FacultyId == id && d.DeletedAt == null);
                var hasUsers = await _context.Users.AnyAsync(u => u.FacultyId == id && u.DeletedAt == null);

                if (hasDepartments || hasUsers)
                {
                    return Json(new { success = false, message = "Không thể xóa khoa này vì còn bộ môn hoặc người dùng!" });
                }

                // Soft delete
                faculty.DeletedAt = DateTime.Now;
                faculty.IsActive = false;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa khoa thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Tạo bộ môn mới
        public async Task<IActionResult> CreateDepartment()
        {
            ViewBag.Faculties = await _context.Faculties
                .Where(f => f.IsActive == true)
                .Select(f => new { f.FacultyId, f.Name, f.Code })
                .ToListAsync();
            return View();
        }

        // POST: Tạo bộ môn mới
        [HttpPost]
        public async Task<IActionResult> CreateDepartment(Department department)
        {
            try
            {
                department.CreatedAt = DateTime.Now;
                department.IsActive = true;

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo bộ môn mới thành công!";
                return RedirectToAction("FacultyManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo bộ môn: {ex.Message}";
                ViewBag.Faculties = await _context.Faculties
                    .Where(f => f.IsActive == true)
                    .Select(f => new { f.FacultyId, f.Name, f.Code })
                    .ToListAsync();
                return View(department);
            }
        }

        // 1.3. THỐNG KÊ HỆ THỐNG
        public async Task<IActionResult> SystemReports()
        {
            var facultyStats = await _context.Faculties
                .Where(f => f.IsActive == true)
                .Select(f => new {
                    FacultyName = f.Name,
                    TotalStudents = f.Users.Count(u => u.RoleId == 4 && u.Status == "Active"),
                    TotalTeachers = f.Users.Count(u => u.RoleId == 3 && u.Status == "Active"),
                    TotalDepartments = f.Departments.Count(d => d.IsActive == true)
                })
                .ToListAsync();

            ViewBag.FacultyStats = facultyStats;
            return View();
        }

        // Reset mật khẩu
        [HttpPost]
        public async Task<IActionResult> ResetPassword(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    // Reset về mật khẩu mặc định
                    user.PasswordHash = "123456";
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Đã reset mật khẩu cho {user.FullName} về: 123456";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi reset mật khẩu: {ex.Message}";
            }

            return RedirectToAction("UserManagement");
        }

        // Khóa/Mở khóa tài khoản
        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.Status = user.Status == "Active" ? "Locked" : "Active";
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Đã {(user.Status == "Active" ? "mở khóa" : "khóa")} tài khoản {user.FullName}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi thay đổi trạng thái tài khoản: {ex.Message}";
            }

            return RedirectToAction("UserManagement");
        }

        // Đăng xuất và về trang Home
        public IActionResult Logout()
        {
            TempData.Clear();
            TempData["SuccessMessage"] = "Đăng xuất thành công!";
            return RedirectToAction("Index", "Home");
        }

        // =================== CHỨC NĂNG MỚI - ADMIN TỔNG ===================

        // GET: Thêm người dùng (tất cả loại tài khoản)
        public async Task<IActionResult> AddUser()
        {
            ViewBag.Roles = await _context.Roles.Select(r => new { r.RoleId, r.RoleName }).ToListAsync();
            ViewBag.Faculties = await _context.Faculties
                .Where(f => f.IsActive == true && f.DeletedAt == null)
                .Select(f => new { f.FacultyId, f.Name, f.Code })
                .ToListAsync();
            ViewBag.Departments = await _context.Departments
                .Where(d => d.IsActive == true && d.DeletedAt == null)
                .Select(d => new { d.DepartmentId, d.Name, d.FacultyId })
                .ToListAsync();
            
            return View();
        }

        // POST: Thêm người dùng
        [HttpPost]
        public async Task<IActionResult> AddUser(User model)
        {
            try
            {
                // Kiểm tra email đã tồn tại
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email && u.DeletedAt == null);
                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = "Email đã tồn tại trong hệ thống!";
                    ViewBag.Faculties = await _context.Faculties
                        .Where(f => f.IsActive == true)
                        .Select(f => new { f.FacultyId, f.Name, f.Code })
                        .ToListAsync();
                    return View(model);
                }

                // Tạo mã tự động dựa trên role
                string codePrefix = "";
                switch (model.RoleId ?? 0)
                {
                    case 1: // Admin Tổng
                        codePrefix = "ADM";
                        break;
                    case 2: // Admin Khoa
                        codePrefix = "MGR";
                        break;
                    case 3: // Giảng viên
                        codePrefix = "GV";
                        break;
                    case 4: // Sinh viên
                        codePrefix = "SV";
                        break;
                }

                var lastUser = await _context.Users
                    .Where(u => u.RoleId == model.RoleId.Value && u.MssvMgv.StartsWith(codePrefix))
                    .OrderByDescending(u => u.MssvMgv)
                    .FirstOrDefaultAsync();
                
                var nextNumber = 1;
                if (lastUser != null)
                {
                    var lastNumberStr = lastUser.MssvMgv.Replace(codePrefix, "");
                    if (int.TryParse(lastNumberStr, out int lastNumber))
                        nextNumber = lastNumber + 1;
                }

                model.MssvMgv = $"{codePrefix}{nextNumber:000}";
                model.PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");
                model.Status = "Active";
                model.CreatedAt = DateTime.Now;

                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                var roleNames = new Dictionary<int, string> { {1, "Admin Tổng"}, {2, "Admin Khoa"}, {3, "Giảng viên"}, {4, "Sinh viên"} };
                var roleName = model.RoleId.HasValue && roleNames.ContainsKey(model.RoleId.Value) ? roleNames[model.RoleId.Value] : "Người dùng";
                TempData["SuccessMessage"] = $"Tạo {roleName} thành công! Mã: {model.MssvMgv}, Mật khẩu: 123456";
                return RedirectToAction("UserManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                ViewBag.Roles = await _context.Roles.Select(r => new { r.RoleId, r.RoleName }).ToListAsync();
                ViewBag.Faculties = await _context.Faculties
                    .Where(f => f.IsActive == true)
                    .Select(f => new { f.FacultyId, f.Name, f.Code })
                    .ToListAsync();
                ViewBag.Departments = await _context.Departments
                    .Where(d => d.IsActive == true)
                    .Select(d => new { d.DepartmentId, d.Name, d.FacultyId })
                    .ToListAsync();
                return View(model);
            }
        }

        // GET: Import cán bộ giảng dạy
        public IActionResult ImportLecturers()
        {
            return View();
        }

        // POST: Import cán bộ giảng dạy từ Excel
        [HttpPost]
        public async Task<IActionResult> ImportLecturers(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel để import!";
                return View();
            }

            try
            {
                // TODO: Implement Excel reading logic
                // Hiện tại chỉ mô phỏng import thành công
                var importedCount = 0; // Số lượng import thành công
                
                TempData["SuccessMessage"] = $"Import thành công {importedCount} cán bộ giảng dạy!";
                TempData["InfoMessage"] = "Chức năng import Excel đang được phát triển. Hiện tại hãy tạo từng giảng viên một cách thủ công.";
                
                return RedirectToAction("UserManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi import: {ex.Message}";
                return View();
            }
        }

        // GET: Reset mật khẩu người dùng cấp cao
        public async Task<IActionResult> ResetPassword()
        {
            // Lấy danh sách user cấp cao (Admin, Admin Khoa, Giảng viên)
            var highLevelUsers = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Faculty)
                .Where(u => u.RoleId <= 3 && u.DeletedAt == null)
                .OrderBy(u => u.RoleId)
                .ThenBy(u => u.FullName)
                .ToListAsync();

            return View(highLevelUsers);
        }

        // POST: Reset mật khẩu cho user cấp cao
        [HttpPost]
        public async Task<IActionResult> ResetPasswordForUser(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null && user.RoleId <= 3) // Chỉ reset cho user cấp cao
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Đã reset mật khẩu cho {user.FullName} về: 123456";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không tìm thấy user hoặc user không thuộc cấp cao!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi reset mật khẩu: {ex.Message}";
            }

            return RedirectToAction("ResetPassword");
        }

        // GET: Admin/Students - Quản lý sinh viên
        public async Task<IActionResult> Students(int page = 1, string search = "", int facultyId = 0)
        {
            var pageSize = 20;
            var query = _context.Users
                .Include(u => u.Faculty)
                .Include(u => u.Role)
                .Where(u => u.RoleId == 4 && u.DeletedAt == null); // RoleId = 4 là Sinh viên

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || 
                                       u.Email.Contains(search) || 
                                       u.MssvMgv.Contains(search));
            }

            // Lọc theo khoa
            if (facultyId > 0)
            {
                query = query.Where(u => u.FacultyId == facultyId);
            }

            var totalCount = await query.CountAsync();
            var students = await query
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.FacultyId = facultyId;
            ViewBag.Faculties = await _context.Faculties
                .Where(f => f.IsActive == true && f.DeletedAt == null)
                .ToListAsync();

            return View(students);
        }

        // GET: Admin/ImportStudents - Form import sinh viên
        public async Task<IActionResult> ImportStudents()
        {
            ViewBag.Faculties = await _context.Faculties
                .Where(f => f.IsActive == true && f.DeletedAt == null)
                .ToListAsync();
            return View();
        }

        // POST: Admin/ImportStudents - Xử lý import
        [HttpPost]
        public async Task<IActionResult> ImportStudents(IFormFile excelFile, int defaultFacultyId)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel!";
                return RedirectToAction("ImportStudents");
            }

            try
            {
                // Force set EPPlus license
                SetEPPlusLicense();
                
                var importResults = new List<string>();
                var successCount = 0;
                var errorCount = 0;

                using (var stream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension?.Rows ?? 0;

                        // Kiểm tra định dạng file (header)
                        var expectedHeaders = new[] { "Mã sinh viên", "Họ tên", "Email", "Số điện thoại", "Khoa" };
                        for (int col = 1; col <= expectedHeaders.Length; col++)
                        {
                            var headerCell = worksheet.Cells[1, col].Value?.ToString();
                            if (headerCell != expectedHeaders[col - 1])
                            {
                                TempData["ErrorMessage"] = $"Định dạng file không đúng. Cột {col} phải là '{expectedHeaders[col - 1]}'";
                                return RedirectToAction("ImportStudents");
                            }
                        }

                        // Lấy danh sách khoa để mapping
                        var faculties = await _context.Faculties
                            .Where(f => f.IsActive == true && f.DeletedAt == null)
                            .ToListAsync();

                        // Xử lý từng dòng dữ liệu
                        for (int row = 2; row <= rowCount; row++)
                        {
                            try
                            {
                                var studentId = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                                var fullName = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                                var email = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                                var phone = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                                var facultyName = worksheet.Cells[row, 5].Value?.ToString()?.Trim();

                                // Validate dữ liệu bắt buộc
                                if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
                                {
                                    importResults.Add($"Dòng {row}: Thiếu thông tin bắt buộc (Mã SV, Họ tên, Email)");
                                    errorCount++;
                                    continue;
                                }

                                // Kiểm tra trùng lặp
                                var existingUser = await _context.Users
                                    .FirstOrDefaultAsync(u => u.Email == email || u.MssvMgv == studentId);
                                
                                if (existingUser != null)
                                {
                                    importResults.Add($"Dòng {row}: Email hoặc Mã SV đã tồn tại ({email}, {studentId})");
                                    errorCount++;
                                    continue;
                                }

                                // Tìm khoa
                                int facultyId = defaultFacultyId;
                                if (!string.IsNullOrEmpty(facultyName))
                                {
                                    var faculty = faculties.FirstOrDefault(f => 
                                        f.Name.Equals(facultyName, StringComparison.OrdinalIgnoreCase) ||
                                        f.Code.Equals(facultyName, StringComparison.OrdinalIgnoreCase));
                                    
                                    if (faculty != null)
                                    {
                                        facultyId = faculty.FacultyId;
                                    }
                                }

                                // Tạo user mới
                                var newUser = new User
                                {
                                    MssvMgv = studentId,
                                    FullName = fullName,
                                    Email = email,
                                    Phone = phone,
                                    RoleId = 4, // Sinh viên
                                    FacultyId = facultyId,
                                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), // Mật khẩu mặc định
                                    Status = "active",
                                    CreatedAt = DateTime.Now,
                                    UpdatedAt = DateTime.Now
                                };

                                _context.Users.Add(newUser);
                                await _context.SaveChangesAsync();

                                importResults.Add($"Dòng {row}: Thành công - {fullName} ({studentId})");
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                importResults.Add($"Dòng {row}: Lỗi - {ex.Message}");
                                errorCount++;
                            }
                        }
                    }
                }

                // Thông báo kết quả
                var resultMessage = $"Import hoàn thành! Thành công: {successCount}, Lỗi: {errorCount}";
                if (successCount > 0)
                {
                    TempData["SuccessMessage"] = resultMessage;
                }
                else
                {
                    TempData["ErrorMessage"] = resultMessage;
                }

                // Lưu chi tiết kết quả vào TempData
                TempData["ImportResults"] = string.Join("\n", importResults);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xử lý file: {ex.Message}";
            }

            return RedirectToAction("ImportStudents");
        }

        // POST: Admin/DeleteStudent - Xóa sinh viên
        [HttpPost]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            try
            {
                var student = await _context.Users.FindAsync(id);
                if (student != null && student.RoleId == 4) // Chỉ xóa sinh viên
                {
                    student.DeletedAt = DateTime.Now;
                    student.Status = "deleted";
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Đã xóa sinh viên {student.FullName}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sinh viên!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa: {ex.Message}";
            }

            return RedirectToAction("Students");
        }

        // GET: Admin/DownloadTemplate - Tải file mẫu Excel
        public IActionResult DownloadTemplate()
        {
            try
            {
                // Force set EPPlus license
                SetEPPlusLicense();
                
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Danh sách sinh viên");

                    // Header
                    worksheet.Cells[1, 1].Value = "Mã sinh viên";
                    worksheet.Cells[1, 2].Value = "Họ tên";
                    worksheet.Cells[1, 3].Value = "Email";
                    worksheet.Cells[1, 4].Value = "Số điện thoại";
                    worksheet.Cells[1, 5].Value = "Khoa";

                    // Định dạng header
                    using (var range = worksheet.Cells[1, 1, 1, 5])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    }

                    // Dữ liệu mẫu
                    worksheet.Cells[2, 1].Value = "SV001";
                    worksheet.Cells[2, 2].Value = "Nguyễn Văn A";
                    worksheet.Cells[2, 3].Value = "nguyenvana@email.com";
                    worksheet.Cells[2, 4].Value = "0123456789";
                    worksheet.Cells[2, 5].Value = "CNTT";

                    worksheet.Cells[3, 1].Value = "SV002";
                    worksheet.Cells[3, 2].Value = "Trần Thị B";
                    worksheet.Cells[3, 3].Value = "tranthib@email.com";
                    worksheet.Cells[3, 4].Value = "0987654321";
                    worksheet.Cells[3, 5].Value = "Kinh tế";

                    // Auto fit columns
                    worksheet.Cells.AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    var fileName = $"MauDanhSachSinhVien_{DateTime.Now:yyyyMMdd}.xlsx";
                    return File(stream.ToArray(), 
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                        fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo file mẫu: {ex.Message}";
                return RedirectToAction("ImportStudents");
            }
        }

        // Helper method to set EPPlus license for version 6
        private void SetEPPlusLicense()
        {
            try
            {
                // For EPPlus 6.x, use LicenseContext property
                var licenseContextProperty = typeof(ExcelPackage).GetProperty("LicenseContext", 
                    BindingFlags.Static | BindingFlags.Public);
                
                if (licenseContextProperty != null)
                {
                    // Get LicenseContext enum type
                    var licenseContextType = licenseContextProperty.PropertyType;
                    
                    // Set to NonCommercial
                    var nonCommercialValue = Enum.Parse(licenseContextType, "NonCommercial");
                    licenseContextProperty.SetValue(null, nonCommercialValue);
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue
                System.Diagnostics.Debug.WriteLine($"EPPlus License Error: {ex.Message}");
            }
        }
    }
}