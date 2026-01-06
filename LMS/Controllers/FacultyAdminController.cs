using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OfficeOpenXml;
using System.Text;

namespace LMS.Controllers
{
    public class FacultyAdminController : BaseController
    {
        private readonly LmsSystemContext _context;

        public FacultyAdminController(LmsSystemContext context)
        {
            _context = context;
        }

        // GET: Faculty Admin Dashboard
        public async Task<IActionResult> Index()
        {
            // Kiểm tra đăng nhập
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn");
            if (isLoggedIn != "true")
            {
                return RedirectToAction("Login", "Home");
            }

            // Lấy FacultyId từ session của user đang đăng nhập
            var facultyIdStr = HttpContext.Session.GetString("FacultyId");
            if (string.IsNullOrEmpty(facultyIdStr) || !int.TryParse(facultyIdStr, out var currentFacultyId))
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            var faculty = await _context.Faculties
                .Include(f => f.Departments)
                .Include(f => f.Users)
                .Include(f => f.Courses)
                .FirstOrDefaultAsync(f => f.FacultyId == currentFacultyId);

            if (faculty == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khoa!";
                return RedirectToAction("Index", "Home");
            }

            // Thống kê tổng quan cho khoa
            ViewBag.FacultyName = faculty.Name;
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.TotalCourses = await _context.Courses
                .CountAsync(c => c.FacultyId == currentFacultyId && c.IsActive == true && c.DeletedAt == null);
            ViewBag.TotalDepartments = await _context.Departments
                .CountAsync(d => d.FacultyId == currentFacultyId && d.IsActive == true && d.DeletedAt == null);
            ViewBag.TotalTeachers = await _context.Users
                .CountAsync(u => u.FacultyId == currentFacultyId && u.RoleId == 3 && u.DeletedAt == null);
            ViewBag.TotalStudents = await _context.Users
                .CountAsync(u => u.FacultyId == currentFacultyId && u.RoleId == 4 && u.DeletedAt == null);

            // Lấy danh sách lớp học phần của khoa
            var facultyClasses = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.Instructor)
                .Include(c => c.Schedules)
                .Where(c => c.Course.FacultyId == currentFacultyId && c.IsActive == true)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToListAsync();
            
            ViewBag.FacultyClasses = facultyClasses;

            return View();
        }

        // Helper method để lấy FacultyId từ session
        private int? GetCurrentFacultyId()
        {
            var facultyIdStr = HttpContext.Session.GetString("FacultyId");
            if (string.IsNullOrEmpty(facultyIdStr) || !int.TryParse(facultyIdStr, out var facultyId))
            {
                return null;
            }
            return facultyId;
        }

        // Helper method để kiểm tra quyền truy cập
        private bool CheckFacultyAccess()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn");
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower()?.Trim();
            
            // Kiểm tra các role có thể truy cập Faculty Admin
            var allowedRoles = new[] { "quản trị khoa", "faculty admin", "quan tri khoa", "departmentadmin" };
            
            return isLoggedIn == "true" && allowedRoles.Contains(userRole);
        }

        // 2.1. QUẢN LÝ KHÓA HỌC
        public async Task<IActionResult> CourseManagement()
        {
            if (!CheckFacultyAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var currentFacultyId = GetCurrentFacultyId();
            if (currentFacultyId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            var courses = await _context.Courses
                .Include(c => c.Faculty)
                .Include(c => c.LeadInstructor)
                .Where(c => c.FacultyId == currentFacultyId.Value && c.DeletedAt == null)
                .ToListAsync();

            // Set thông tin cho layout
            var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
            ViewBag.FacultyName = faculty?.Name ?? "";
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View(courses);
        }

        // GET: Tạo khóa học mới
        public async Task<IActionResult> CreateCourse()
        {
            if (!CheckFacultyAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var currentFacultyId = GetCurrentFacultyId();
            if (currentFacultyId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            // Lấy danh sách giảng viên của khoa
            var teachers = await _context.Users
                .Where(u => u.FacultyId == currentFacultyId.Value && u.RoleId == 3 && u.DeletedAt == null)
                .ToListAsync();

            // Lấy danh sách bộ môn của khoa
            var departments = await _context.Departments
                .Where(d => d.FacultyId == currentFacultyId.Value && d.IsActive == true && d.DeletedAt == null)
                .ToListAsync();

            ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
            ViewBag.Departments = new SelectList(departments ?? new List<Department>(), "DepartmentId", "Name");
            ViewBag.CurrentFacultyId = currentFacultyId;

            return View();
        }

        // POST: Tạo khóa học mới
        [HttpPost]
        public async Task<IActionResult> CreateCourse(Course course, int? InstructorId)
        {
            try
            {
                // Set required fields
                course.CreatedAt = DateTime.Now;
                course.IsActive = course.IsActive ?? true;
                course.DeletedAt = null;
                
                // Set academic year and semester (current)
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;
                course.AcademicYear = $"{currentYear}-{currentYear + 1}";
                course.Semester = currentMonth >= 9 || currentMonth <= 1 ? "HK1" : "HK2";
                
                // Set lead instructor (from form or current user)
                var currentUserId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
                course.LeadInstructorId = InstructorId ?? currentUserId;
                
                // Auto-generate course code if empty
                if (string.IsNullOrWhiteSpace(course.Code))
                {
                    var lastCourse = await _context.Courses
                        .Where(c => c.FacultyId == course.FacultyId)
                        .OrderByDescending(c => c.CourseId)
                        .FirstOrDefaultAsync();
                    
                    var nextNumber = (lastCourse?.CourseId ?? 0) + 1;
                    course.Code = $"CRS{nextNumber:D3}";
                }

                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Tạo khóa học '{course.Name}' thành công!";
                return RedirectToAction("CourseManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo khóa học: {ex.Message}. Chi tiết: {ex.InnerException?.Message}";
                
                // Reload dropdown data
                var currentFacultyId = course.FacultyId;
                var teachers = await _context.Users
                    .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 3 && u.DeletedAt == null)
                    .ToListAsync();
                
                var departments = await _context.Departments
                    .Where(d => d.FacultyId == currentFacultyId && d.IsActive == true && d.DeletedAt == null)
                    .ToListAsync();

                ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
                ViewBag.Departments = new SelectList(departments ?? new List<Department>(), "DepartmentId", "Name");
                ViewBag.CurrentFacultyId = currentFacultyId;

                return View(course);
            }
        }

        // GET: Chỉnh sửa khóa học
        public async Task<IActionResult> EditCourse(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Faculty)
                .FirstOrDefaultAsync(c => c.CourseId == id);

            if (course == null || course.DeletedAt != null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khóa học!";
                return RedirectToAction("CourseManagement");
            }

            // Lấy danh sách giảng viên của khoa
            var teachers = await _context.Users
                .Where(u => u.FacultyId == course.FacultyId && u.RoleId == 3 && u.DeletedAt == null)
                .ToListAsync();

            ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName", course.LeadInstructorId);

            return View(course);
        }

        // POST: Chỉnh sửa khóa học
        [HttpPost]
        public async Task<IActionResult> EditCourse(Course course)
        {
            try
            {
                var existingCourse = await _context.Courses.FindAsync(course.CourseId);
                if (existingCourse == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy khóa học!";
                    return View(course);
                }

                existingCourse.Name = course.Name;
                existingCourse.Code = course.Code;
                existingCourse.Description = course.Description;
                existingCourse.Credits = course.Credits;
                existingCourse.Duration = course.Duration;
                existingCourse.LeadInstructorId = course.LeadInstructorId;
                existingCourse.IsActive = course.IsActive;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật khóa học thành công!";
                return RedirectToAction("CourseManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật khóa học: {ex.Message}";
                
                // Reload dropdown data
                var teachers = await _context.Users
                    .Where(u => u.FacultyId == course.FacultyId && u.RoleId == 3 && u.DeletedAt == null)
                    .ToListAsync();
                ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName", course.LeadInstructorId);

                return View(course);
            }
        }

        // POST: Cập nhật tên khóa học qua AJAX
        [HttpPost]
        public async Task<IActionResult> UpdateCourseName([FromBody] UpdateCourseNameRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Json(new { success = false, message = "Tên khóa học không được để trống!" });
                }

                var course = await _context.Courses.FindAsync(request.CourseId);
                if (course == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khóa học!" });
                }

                course.Name = request.Name.Trim();
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Cập nhật tên khóa học thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // POST: Xóa khóa học
        [HttpPost]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            try
            {
                var course = await _context.Courses.FindAsync(id);
                if (course == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy khóa học!";
                    return RedirectToAction("CourseManagement");
                }

                course.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Xóa khóa học thành công!";
                return RedirectToAction("CourseManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa khóa học: {ex.Message}";
                return RedirectToAction("CourseManagement");
            }
        }
        
        // Class để nhận dữ liệu từ AJAX request
        public class UpdateCourseNameRequest
        {
            public int CourseId { get; set; }
            public string Name { get; set; }
        }

        // 2.2. QUẢN LÝ GIẢNG VIÊN
        public async Task<IActionResult> TeacherManagement()
        {
            var currentFacultyId = 1; // TODO: Lấy từ session

            var teachers = await _context.Users
                .Include(u => u.Faculty)
                .Include(u => u.Department)
                .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 3 && u.DeletedAt == null)
                .ToListAsync();

            return View(teachers);
        }

        // GET: Thêm giảng viên vào khoa
        public async Task<IActionResult> AddTeacher()
        {
            var currentFacultyId = 1; // TODO: Lấy từ session

            var departments = await _context.Departments
                .Where(d => d.FacultyId == currentFacultyId && d.IsActive == true)
                .ToListAsync();

            ViewBag.Departments = new SelectList(departments, "DepartmentId", "Name");
            ViewBag.CurrentFacultyId = currentFacultyId;

            return View();
        }

        // POST: Thêm giảng viên vào khoa
        [HttpPost]
        public async Task<IActionResult> AddTeacher(User teacher)
        {
            try
            {
                // Kiểm tra mã giảng viên đã tồn tại chưa
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.MssvMgv == teacher.MssvMgv && u.DeletedAt == null);

                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = "Mã giảng viên đã tồn tại!";
                    
                    // Reload dropdown data
                    var departments = await _context.Departments
                        .Where(d => d.FacultyId == teacher.FacultyId && d.IsActive == true)
                        .ToListAsync();
                    ViewBag.Departments = new SelectList(departments, "DepartmentId", "Name");
                    ViewBag.CurrentFacultyId = teacher.FacultyId;

                    return View(teacher);
                }

                teacher.RoleId = 3; // Giảng viên
                teacher.CreatedAt = DateTime.Now;
                teacher.Status = "Active";
                teacher.DeletedAt = null;
                teacher.PasswordHash = "123456"; // Mật khẩu mặc định

                _context.Users.Add(teacher);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Thêm giảng viên thành công!";
                return RedirectToAction("TeacherManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi thêm giảng viên: {ex.Message}";
                
                // Reload dropdown data
                var departments = await _context.Departments
                    .Where(d => d.FacultyId == teacher.FacultyId && d.IsActive == true)
                    .ToListAsync();
                ViewBag.Departments = new SelectList(departments, "DepartmentId", "Name");
                ViewBag.CurrentFacultyId = teacher.FacultyId;

                return View(teacher);
            }
        }

        // 2.3. QUẢN LÝ SINH VIÊN
        public async Task<IActionResult> StudentManagement()
        {
            // Kiểm tra đăng nhập
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn");
            if (isLoggedIn != "true")
            {
                return RedirectToAction("Login", "Home");
            }

            // Lấy FacultyId từ session
            var facultyIdStr = HttpContext.Session.GetString("FacultyId");
            if (string.IsNullOrEmpty(facultyIdStr) || !int.TryParse(facultyIdStr, out var currentFacultyId))
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            var students = await _context.Users
                .Include(u => u.Faculty)
                .Include(u => u.Department)
                .Include(u => u.ClassStudents)
                    .ThenInclude(cs => cs.Class)
                    .ThenInclude(c => c.Course)
                .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 4 && u.DeletedAt == null)
                .ToListAsync();

            // Lấy danh sách các lớp thuộc khoa để hiển thị trong filter
            var classes = await _context.Classes
                .Include(c => c.Course)
                .Where(c => c.Course.FacultyId == currentFacultyId && c.DeletedAt == null)
                .ToListAsync();

            // Lấy danh sách bộ môn thuộc khoa
            var departments = await _context.Departments
                .Where(d => d.FacultyId == currentFacultyId && d.IsActive == true && d.DeletedAt == null)
                .ToListAsync();

            // Lấy danh sách lớp hành chính (StudentClass) để hiển thị trong filter
            var studentClasses = students
                .Where(s => !string.IsNullOrEmpty(s.StudentClass))
                .Select(s => s.StudentClass)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            ViewBag.Classes = classes ?? new List<Class>();
            ViewBag.Departments = departments ?? new List<Department>();
            ViewBag.StudentClasses = studentClasses ?? new List<string>();

            return View(students);
        }

        // POST: Assign Department to Student
        [HttpPost]
        public async Task<IActionResult> AssignDepartment(int studentId, int departmentId)
        {
            try
            {
                var student = await _context.Users.FindAsync(studentId);
                var department = await _context.Departments.FindAsync(departmentId);

                if (student == null || department == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sinh viên hoặc chuyên ngành!";
                    return RedirectToAction("StudentManagement");
                }

                // Kiểm tra quyền - chỉ được phân bộ môn trong khoa của mình
                var facultyIdStr = HttpContext.Session.GetString("FacultyId");
                if (!string.IsNullOrEmpty(facultyIdStr) && int.TryParse(facultyIdStr, out var currentFacultyId))
                {
                    if (student.FacultyId != currentFacultyId || department.FacultyId != currentFacultyId)
                    {
                        TempData["ErrorMessage"] = "Bạn chỉ có thể phân chuyên ngành cho sinh viên trong khoa của mình!";
                        return RedirectToAction("StudentManagement");
                    }
                }

                student.DepartmentId = departmentId;
                student.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã phân sinh viên {student.FullName} vào chuyên ngành {department.Name}!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi phân chuyên ngành: {ex.Message}";
            }

            return RedirectToAction("StudentManagement");
        }

        // POST: Update student's administrative class
        [HttpPost]
        public async Task<IActionResult> UpdateStudentClass(int studentId, string studentClass)
        {
            try
            {
                var student = await _context.Users.FindAsync(studentId);
                if (student == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sinh viên!";
                    return RedirectToAction("StudentManagement");
                }

                // Cập nhật lớp hành chính
                student.StudentClass = studentClass?.Trim();
                student.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã cập nhật lớp hành chính cho sinh viên {student.FullName} thành '{studentClass}'!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật lớp: {ex.Message}";
            }

            return RedirectToAction("StudentManagement");
        }

        // POST: Delete student account
        [HttpPost]
        public async Task<IActionResult> DeleteStudent(int studentId)
        {
            try
            {
                var student = await _context.Users.FindAsync(studentId);
                if (student == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sinh viên!";
                    return RedirectToAction("StudentManagement");
                }

                // Kiểm tra quyền: chỉ được xóa sinh viên thuộc khoa mình
                var facultyIdStr = HttpContext.Session.GetString("FacultyId");
                if (!string.IsNullOrEmpty(facultyIdStr) && int.TryParse(facultyIdStr, out var currentFacultyId))
                {
                    if (student.FacultyId != currentFacultyId)
                    {
                        TempData["ErrorMessage"] = "Bạn không có quyền xóa sinh viên của khoa khác!";
                        return RedirectToAction("StudentManagement");
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                    return RedirectToAction("StudentManagement");
                }

                // Soft delete: sử dụng raw SQL để tránh conflict với trigger
                // Chỉ set DeletedAt, không thay đổi Status để tránh conflict với CHECK constraint
                // Thêm timestamp vào email và MSSV để tránh conflict với UNIQUE constraint khi import lại
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                await _context.Database.ExecuteSqlRawAsync(
                    @"UPDATE Users 
                      SET DeletedAt = GETDATE(), 
                          UpdatedAt = GETDATE(),
                          Email = Email + '_deleted_' + {1},
                          MSSV_MGV = MSSV_MGV + '_deleted_' + {1}
                      WHERE UserID = {0}",
                    studentId, timestamp
                );

                TempData["SuccessMessage"] = $"Đã xóa tài khoản sinh viên {student.FullName} (MSSV: {student.MssvMgv})!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa tài khoản: {ex.Message}";
            }

            return RedirectToAction("StudentManagement");
        }

        // GET: Import sinh viên từ Excel
        public async Task<IActionResult> ImportStudents()
        {
            var currentFacultyId = GetCurrentFacultyId();
            if (currentFacultyId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            // Lấy danh sách lớp hành chính để chọn
            var administrativeClasses = await _context.AdministrativeClasses
                .Where(ac => ac.FacultyId == currentFacultyId.Value && ac.DeletedAt == null && ac.IsActive == true)
                .OrderBy(ac => ac.Name)
                .ToListAsync();

            // Lấy danh sách chuyên ngành của khoa để hiển thị
            var departments = await _context.Departments
                .Where(d => d.FacultyId == currentFacultyId.Value && d.IsActive == true && d.DeletedAt == null)
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.AdministrativeClasses = new SelectList(administrativeClasses, "AdministrativeClassId", "Name");
            ViewBag.Departments = departments;

            var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
            ViewBag.FacultyName = faculty?.Name ?? "";
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View();
        }

        // POST: Import sinh viên từ Excel
        [HttpPost]
        public async Task<IActionResult> ImportStudents(IFormFile excelFile, int? administrativeClassId)
        {
            var currentFacultyId = GetCurrentFacultyId();
            if (currentFacultyId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn! Vui lòng đăng nhập lại.";
                return RedirectToAction("Index", "Home");
            }

            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel!";
                return RedirectToAction("ImportStudents");
            }

            try
            {
                // Set license context for EPPlus
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                
                // Log FacultyId để debug
                var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
                Console.WriteLine($"[Import] Faculty Admin đang import cho khoa: {faculty?.Name} (ID: {currentFacultyId})");

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
                        var colCount = worksheet.Dimension?.Columns ?? 0;

                        if (rowCount < 2)
                        {
                            TempData["ErrorMessage"] = "File Excel không có dữ liệu!";
                            return RedirectToAction("ImportStudents");
                        }

                        // Kiểm tra định dạng file (header) - chỉ check 3 cột bắt buộc
                        var requiredHeaders = new[] { "Mã sinh viên", "Họ tên", "Email" };
                        for (int col = 1; col <= requiredHeaders.Length; col++)
                        {
                            var headerCell = worksheet.Cells[1, col].Value?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(headerCell))
                            {
                                TempData["ErrorMessage"] = $"Cột {col} không có tiêu đề. Vui lòng kiểm tra lại file Excel!";
                                return RedirectToAction("ImportStudents");
                            }
                            // Kiểm tra tương đối (contains) thay vì tuyệt đối
                            if (!headerCell.Contains("Mã") && !headerCell.Contains("SV") && col == 1)
                            {
                                TempData["ErrorMessage"] = $"Cột 1 phải là cột Mã sinh viên (hiện tại: '{headerCell}')";
                                return RedirectToAction("ImportStudents");
                            }
                            if (!headerCell.Contains("tên") && !headerCell.Contains("Tên") && col == 2)
                            {
                                TempData["ErrorMessage"] = $"Cột 2 phải là cột Họ tên (hiện tại: '{headerCell}')";
                                return RedirectToAction("ImportStudents");
                            }
                            if (!headerCell.Contains("Email") && !headerCell.Contains("email") && col == 3)
                            {
                                TempData["ErrorMessage"] = $"Cột 3 phải là cột Email (hiện tại: '{headerCell}')";
                                return RedirectToAction("ImportStudents");
                            }
                        }

                        // Lấy danh sách chuyên ngành và lớp hành chính CỦA KHOA HIỆN TẠI
                        var departments = await _context.Departments
                            .Where(d => d.FacultyId == currentFacultyId.Value && d.IsActive == true && d.DeletedAt == null)
                            .ToListAsync();
                        
                        // Lấy TẤT CẢ departments để kiểm tra xem có chuyên ngành nào khớp nhưng thuộc khoa khác không
                        var allDepartments = await _context.Departments
                            .Include(d => d.Faculty)
                            .Where(d => d.IsActive == true && d.DeletedAt == null)
                            .ToListAsync();

                        var administrativeClasses = await _context.AdministrativeClasses
                            .Where(ac => ac.FacultyId == currentFacultyId.Value && ac.IsActive == true && ac.DeletedAt == null)
                            .ToListAsync();

                        // Xử lý từng dòng
                        for (int row = 2; row <= rowCount; row++)
                        {
                            try
                            {
                                // Đọc giá trị từ các cell
                                var cell1 = worksheet.Cells[row, 1].Value;
                                var cell2 = worksheet.Cells[row, 2].Value;
                                var cell3 = worksheet.Cells[row, 3].Value;
                                var cell4 = worksheet.Cells[row, 4].Value;
                                var cell5 = worksheet.Cells[row, 5].Value;

                                // Bỏ qua dòng trống hoàn toàn
                                if (cell1 == null && cell2 == null && cell3 == null)
                                {
                                    continue;
                                }

                                var studentId = cell1?.ToString()?.Trim();
                                var fullName = cell2?.ToString()?.Trim();
                                var email = cell3?.ToString()?.Trim();
                                var phone = cell4?.ToString()?.Trim();
                                var studentClass = cell5?.ToString()?.Trim();
                                
                                // Hỗ trợ cả file 7 cột (cũ) và 8 cột (mới)
                                string facultyCode = null;
                                string facultyName = null;
                                string departmentName = null;
                                
                                if (colCount >= 8)
                                {
                                    // File mới: có cột Mã khoa
                                    facultyCode = worksheet.Cells[row, 6].Value?.ToString()?.Trim();
                                    facultyName = worksheet.Cells[row, 7].Value?.ToString()?.Trim();
                                    departmentName = worksheet.Cells[row, 8].Value?.ToString()?.Trim();
                                }
                                else if (colCount >= 7)
                                {
                                    // File cũ: không có cột Mã khoa
                                    facultyName = worksheet.Cells[row, 6].Value?.ToString()?.Trim();
                                    departmentName = worksheet.Cells[row, 7].Value?.ToString()?.Trim();
                                }

                                // Dừng lại nếu gặp dòng hướng dẫn (bắt đầu bằng "Lưu ý", "-", hoặc không phải dữ liệu)
                                if (!string.IsNullOrEmpty(studentId) && 
                                    (studentId.StartsWith("Lưu ý", StringComparison.OrdinalIgnoreCase) ||
                                     studentId.StartsWith("-") ||
                                     studentId.StartsWith("Chú ý", StringComparison.OrdinalIgnoreCase) ||
                                     studentId.StartsWith("Ghi chú", StringComparison.OrdinalIgnoreCase) ||
                                     studentId.Contains("cột bắt buộc", StringComparison.OrdinalIgnoreCase)))
                                {
                                    break; // Dừng import, phần còn lại là hướng dẫn
                                }

                                // Validate
                                if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
                                {
                                    importResults.Add($"Dòng {row}: Thiếu thông tin bắt buộc (Mã SV, Họ tên, Email)");
                                    errorCount++;
                                    continue;
                                }

                                // Kiểm tra trùng lặp - check tất cả user (kể cả đã xóa) vì có UNIQUE constraint
                                var existingUser = await _context.Users
                                    .FirstOrDefaultAsync(u => u.Email == email || u.MssvMgv == studentId);

                                if (existingUser != null)
                                {
                                    // Nếu user đã bị xóa, có thể khôi phục hoặc bỏ qua
                                    if (existingUser.DeletedAt != null)
                                    {
                                        importResults.Add($"Dòng {row}: Email hoặc Mã SV đã tồn tại (đã xóa trước đó) ({email}, {studentId})");
                                    }
                                    else
                                    {
                                        importResults.Add($"Dòng {row}: Email hoặc Mã SV đã tồn tại ({email}, {studentId})");
                                    }
                                    errorCount++;
                                    continue;
                                }

                                // Tìm hoặc tạo lớp hành chính
                                int? finalAdminClassId = administrativeClassId;
                                string classSearchInfo = "";
                                if (!string.IsNullOrEmpty(studentClass))
                                {
                                    // Tìm lớp hành chính hiện có
                                    var adminClass = administrativeClasses.FirstOrDefault(ac =>
                                        ac.Code.Equals(studentClass, StringComparison.OrdinalIgnoreCase));
                                    
                                    if (adminClass != null)
                                    {
                                        finalAdminClassId = adminClass.AdministrativeClassId;
                                        classSearchInfo = $"Tìm thấy lớp {adminClass.Name}";
                                    }
                                    else
                                    {
                                        // TỰ ĐỘNG TẠO lớp hành chính mới nếu chưa tồn tại
                                        try
                                        {
                                            // Trích xuất năm từ mã lớp (VD: CNTT2026C -> 2026)
                                            var yearMatch = System.Text.RegularExpressions.Regex.Match(studentClass, @"(\d{4})");
                                            var academicYear = yearMatch.Success ? yearMatch.Groups[1].Value : DateTime.Now.Year.ToString();
                                            
                                            // Lấy UserId từ session, nếu không có thì để null
                                            int? createdById = null;
                                            var userIdStr = HttpContext.Session.GetString("UserId");
                                            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var userId))
                                            {
                                                createdById = userId;
                                            }
                                            
                                            var newAdminClass = new AdministrativeClass
                                            {
                                                Code = studentClass,
                                                Name = studentClass, // Có thể tùy chỉnh format name
                                                FacultyId = currentFacultyId.Value,
                                                AcademicYear = academicYear,
                                                MaxStudents = 50, // Mặc định
                                                CurrentStudents = 0,
                                                IsActive = true,
                                                CreatedAt = DateTime.Now,
                                                CreatedBy = createdById
                                            };
                                            
                                            _context.AdministrativeClasses.Add(newAdminClass);
                                            await _context.SaveChangesAsync();
                                            
                                            // Thêm vào danh sách để các dòng sau có thể dùng
                                            administrativeClasses.Add(newAdminClass);
                                            
                                            finalAdminClassId = newAdminClass.AdministrativeClassId;
                                            classSearchInfo = $"Đã tạo mới lớp '{studentClass}'";
                                        }
                                        catch (Exception ex)
                                        {
                                            classSearchInfo = $"Không thể tạo lớp '{studentClass}': {ex.Message}";
                                            finalAdminClassId = null;
                                        }
                                    }
                                }

                                // Tìm chuyên ngành
                                int? departmentId = null;
                                string departmentSearchInfo = "";
                                
                                if (!string.IsNullOrEmpty(departmentName))
                                {
                                    // Thử tìm TRONG KHOA HIỆN TẠI trước
                                    var department = departments.FirstOrDefault(d =>
                                        d.Name.Equals(departmentName, StringComparison.OrdinalIgnoreCase) ||
                                        d.Code.Equals(departmentName, StringComparison.OrdinalIgnoreCase));
                                    
                                    // Nếu không tìm thấy, thử tìm kiếm gần đúng TRONG KHOA HIỆN TẠI
                                    if (department == null)
                                    {
                                        department = departments.FirstOrDefault(d =>
                                            d.Name.Contains(departmentName, StringComparison.OrdinalIgnoreCase) ||
                                            departmentName.Contains(d.Name, StringComparison.OrdinalIgnoreCase) ||
                                            d.Code.Contains(departmentName, StringComparison.OrdinalIgnoreCase));
                                    }
                                    
                                    if (department != null)
                                    {
                                        departmentId = department.DepartmentId;
                                        departmentSearchInfo = $"Tìm thấy: {department.Name}";
                                    }
                                    else
                                    {
                                        // Kiểm tra xem có chuyên ngành này trong khoa khác không
                                        var deptInOtherFaculty = allDepartments.FirstOrDefault(d =>
                                            d.FacultyId != currentFacultyId.Value &&
                                            (d.Name.Contains(departmentName, StringComparison.OrdinalIgnoreCase) ||
                                             departmentName.Contains(d.Name, StringComparison.OrdinalIgnoreCase) ||
                                             d.Code.Contains(departmentName, StringComparison.OrdinalIgnoreCase)));
                                        
                                        if (deptInOtherFaculty != null)
                                        {
                                            departmentSearchInfo = $"Không tìm thấy '{departmentName}' trong khoa của bạn (tìm thấy ở {deptInOtherFaculty.Faculty?.Name})";
                                        }
                                        else
                                        {
                                            departmentSearchInfo = $"Không tìm thấy '{departmentName}'";
                                        }
                                    }
                                }

                                // Nếu không có chuyên ngành HOẶC không tìm thấy, tự động phân đều
                                if (!departmentId.HasValue && departments.Any())
                                {
                                    var deptStudentCounts = await _context.Users
                                        .Where(u => u.RoleId == 4 && u.FacultyId == currentFacultyId.Value && u.DepartmentId.HasValue)
                                        .GroupBy(u => u.DepartmentId)
                                        .Select(g => new { DeptId = g.Key, Count = g.Count() })
                                        .ToListAsync();

                                    var targetDept = departments
                                        .Select(d => new
                                        {
                                            Dept = d,
                                            StudentCount = deptStudentCounts.FirstOrDefault(c => c.DeptId == d.DepartmentId)?.Count ?? 0
                                        })
                                        .OrderBy(x => x.StudentCount)
                                        .First().Dept;

                                    departmentId = targetDept.DepartmentId;
                                }

                                // Tạo user mới
                                var newUser = new User
                                {
                                    MssvMgv = studentId,
                                    FullName = fullName,
                                    Email = email,
                                    Phone = phone,
                                    StudentClass = studentClass,
                                    AdministrativeClassId = finalAdminClassId,
                                    RoleId = 4, // Sinh viên
                                    FacultyId = currentFacultyId.Value,
                                    DepartmentId = departmentId,
                                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                                    Status = "Active",
                                    CreatedAt = DateTime.Now,
                                    UpdatedAt = DateTime.Now
                                };

                                _context.Users.Add(newUser);
                                await _context.SaveChangesAsync();

                                // Cập nhật số lượng sinh viên trong lớp hành chính
                                if (finalAdminClassId.HasValue)
                                {
                                    var adminClass = await _context.AdministrativeClasses.FindAsync(finalAdminClassId.Value);
                                    if (adminClass != null)
                                    {
                                        adminClass.CurrentStudents = await _context.Users
                                            .CountAsync(u => u.AdministrativeClassId == finalAdminClassId.Value && u.DeletedAt == null);
                                        await _context.SaveChangesAsync();
                                    }
                                }

                                var deptInfo = departmentId.HasValue ?
                                    departments.FirstOrDefault(d => d.DepartmentId == departmentId)?.Name ?? "Tự động phân" :
                                    "Tự động phân";
                                var classInfo = !string.IsNullOrEmpty(studentClass) ? studentClass : "Không có";
                                
                                // Thêm thông tin tìm kiếm
                                var detailInfo = "";
                                if (!string.IsNullOrEmpty(departmentSearchInfo))
                                {
                                    detailInfo += $" [CN: {departmentSearchInfo}]";
                                }
                                if (!string.IsNullOrEmpty(classSearchInfo))
                                {
                                    detailInfo += $" [Lớp: {classSearchInfo}]";
                                }

                                importResults.Add($"Dòng {row}: Thành công - {fullName} ({studentId}) - Lớp: {classInfo}, Chuyên ngành: {deptInfo}{detailInfo}");
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

                // Tạo thông báo kết quả
                TempData["SuccessMessage"] = $"Import thành công {successCount} sinh viên, {errorCount} dòng lỗi.";
                TempData["ImportDetails"] = string.Join("<br>", importResults.Take(11));

                return RedirectToAction("StudentManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi import: {ex.Message}";
                return RedirectToAction("ImportStudents");
            }
        }

        // GET: Tải file Excel mẫu để import sinh viên
        public IActionResult DownloadImportTemplate()
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Danh sách sinh viên");

                    // Header
                    worksheet.Cells[1, 1].Value = "Mã sinh viên";
                    worksheet.Cells[1, 2].Value = "Họ tên";
                    worksheet.Cells[1, 3].Value = "Email";
                    worksheet.Cells[1, 4].Value = "Số điện thoại";
                    worksheet.Cells[1, 5].Value = "Lớp hành chính";
                    worksheet.Cells[1, 6].Value = "Mã khoa";
                    worksheet.Cells[1, 7].Value = "Khoa";
                    worksheet.Cells[1, 8].Value = "Chuyên ngành";

                    // Style header
                    using (var range = worksheet.Cells[1, 1, 1, 8])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                        range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }

                    // Dữ liệu mẫu
                    worksheet.Cells[2, 1].Value = "SV001";
                    worksheet.Cells[2, 2].Value = "Nguyễn Văn An";
                    worksheet.Cells[2, 3].Value = "an.nguyen@student.lms.edu.vn";
                    worksheet.Cells[2, 4].Value = "0901234567";
                    worksheet.Cells[2, 5].Value = "CNTT2021A";
                    worksheet.Cells[2, 6].Value = "CNTT";
                    worksheet.Cells[2, 7].Value = "Khoa Công nghệ Thông tin";
                    worksheet.Cells[2, 8].Value = "Công nghệ Phần mềm";

                    worksheet.Cells[3, 1].Value = "SV002";
                    worksheet.Cells[3, 2].Value = "Trần Thị Bình";
                    worksheet.Cells[3, 3].Value = "binh.tran@student.lms.edu.vn";
                    worksheet.Cells[3, 4].Value = "0912345678";
                    worksheet.Cells[3, 5].Value = "CNTT2021A";
                    worksheet.Cells[3, 6].Value = "CNTT";
                    worksheet.Cells[3, 7].Value = "Khoa Công nghệ Thông tin";
                    worksheet.Cells[3, 8].Value = "Hệ thống Thông tin";

                    worksheet.Cells[4, 1].Value = "SV003";
                    worksheet.Cells[4, 2].Value = "Lê Văn Cường";
                    worksheet.Cells[4, 3].Value = "cuong.le@student.lms.edu.vn";
                    worksheet.Cells[4, 4].Value = "0923456789";
                    worksheet.Cells[4, 5].Value = "CNTT2021B";
                    worksheet.Cells[4, 6].Value = "CNTT";
                    worksheet.Cells[4, 7].Value = "Khoa Công nghệ Thông tin";
                    worksheet.Cells[4, 8].Value = "Khoa học Máy tính";

                    // Auto fit columns
                    worksheet.Cells.AutoFitColumns();

                    // Thêm note (để cách 1 dòng)
                    worksheet.Cells[5, 1].Value = "Lưu ý:";
                    worksheet.Cells[5, 1].Style.Font.Bold = true;
                    worksheet.Cells[5, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    
                    worksheet.Cells[6, 1].Value = "- Các cột bắt buộc: Mã sinh viên, Họ tên, Email";
                    worksheet.Cells[7, 1].Value = "- Mật khẩu mặc định của sinh viên là: 123456";
                    worksheet.Cells[8, 1].Value = "- Lớp hành chính: Mã của lớp hành chính (VD: CNTT2021A)";
                    worksheet.Cells[9, 1].Value = "- Mã khoa: Mã viết tắt của khoa (VD: CNTT, KT, QTKD)";
                    worksheet.Cells[10, 1].Value = "- Khoa: Tên đầy đủ khoa (có thể bỏ trống)";
                    worksheet.Cells[11, 1].Value = "- Chuyên ngành: Tên chuyên ngành/bộ môn (phải trùng khớp với tên trong hệ thống)";
                    worksheet.Cells[12, 1].Value = "- Nếu không điền chuyên ngành, hệ thống sẽ tự động phân đều";
                    worksheet.Cells[13, 1].Value = "- Xóa các dòng hướng dẫn này trước khi import hoặc để nguyên, hệ thống sẽ tự động bỏ qua";
                    
                    worksheet.Cells[6, 1, 13, 1].Style.WrapText = true;
                    worksheet.Cells[6, 1, 13, 1].Style.Font.Italic = true;
                    worksheet.Cells[6, 1, 13, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);

                    var stream = new MemoryStream(package.GetAsByteArray());
                    var fileName = $"Import-Students-Template-{DateTime.Now:yyyyMMdd}.xlsx";

                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo file mẫu: {ex.Message}";
                return RedirectToAction("ImportStudents");
            }
        }

        // 2.4. BÁO CÁO - THỐNG KÊ
        public async Task<IActionResult> Reports()
        {
            var currentFacultyId = GetCurrentFacultyId();
            if (currentFacultyId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            // Báo cáo tình trạng lớp học
            var classReports = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.ClassStudents)
                .Where(c => c.Course.FacultyId == currentFacultyId.Value && c.DeletedAt == null)
                .Select(c => new {
                    ClassName = c.Name,
                    CourseName = c.Course.Name,
                    TotalStudents = c.ClassStudents.Count(),
                    Capacity = c.MaxStudents ?? 0,
                    Status = c.IsActive == true ? "Đang hoạt động" : "Tạm dừng"
                })
                .ToListAsync();

            // Báo cáo kết quả học tập
            var gradeReports = await _context.StudentGrades
                .Include(sg => sg.Student)
                .Include(sg => sg.Class)
                    .ThenInclude(c => c.Course)
                .Where(sg => sg.Class.Course.FacultyId == currentFacultyId.Value)
                .GroupBy(sg => sg.Class.Course.Name)
                .Select(g => new {
                    CourseName = g.Key,
                    TotalStudents = g.Count(),
                    AverageGrade = g.Average(sg => sg.FinalScore ?? 0),
                    PassRate = g.Count(sg => (sg.FinalScore ?? 0) >= 5.0m) * 100.0 / g.Count()
                })
                .ToListAsync();

            ViewBag.ClassReports = classReports;
            ViewBag.GradeReports = gradeReports;

            return View();
        }



        // ===== CLASS MANAGEMENT =====
        
        // GET: Quản lý lớp học
        public async Task<IActionResult> ClassManagement()
        {
            if (!CheckFacultyAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var currentFacultyId = GetCurrentFacultyId();
            if (currentFacultyId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            var classes = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.Instructor)
                .Include(c => c.ClassStudents)
                .Where(c => c.Course.FacultyId == currentFacultyId.Value && c.DeletedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
            ViewBag.FacultyName = faculty?.Name ?? "";
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View(classes);
        }

        // GET: Tạo lớp học mới
        public async Task<IActionResult> CreateClass()
        {
            if (!CheckFacultyAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var currentFacultyId = GetCurrentFacultyId();
            if (currentFacultyId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            // Load courses của khoa
            var courses = await _context.Courses
                .Where(c => c.FacultyId == currentFacultyId.Value && c.DeletedAt == null && c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // Load giảng viên của khoa
            var teachers = await _context.Users
                .Where(u => u.FacultyId == currentFacultyId.Value && u.RoleId == 3 && u.DeletedAt == null)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // Load danh sách học kỳ đã được cấu hình
            var semesters = await _context.SemesterConfigs
                .Where(s => s.IsActive == true)
                .OrderByDescending(s => s.AcademicYear)
                .ThenBy(s => s.SemesterNumber)
                .ToListAsync();

            ViewBag.Courses = new SelectList(courses, "CourseId", "Name");
            ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
            ViewBag.Semesters = semesters;
            ViewBag.CurrentFacultyId = currentFacultyId.Value;
            ViewBag.CoursesData = courses.Select(c => new { c.CourseId, c.Name, c.Credits }).ToList();

            var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
            ViewBag.FacultyName = faculty?.Name ?? "";
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View();
        }

        // POST: Tạo lớp học mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(Class classModel)
        {
            try
            {
                // Set required fields
                classModel.CreatedAt = DateTime.Now;
                classModel.IsActive = classModel.IsActive ?? true;
                classModel.DeletedAt = null;
                classModel.CurrentStudents = 0;

                // Auto-generate class code if empty
                if (string.IsNullOrWhiteSpace(classModel.Code))
                {
                    var course = await _context.Courses.FindAsync(classModel.CourseId);
                    var lastClass = await _context.Classes
                        .Where(c => c.CourseId == classModel.CourseId)
                        .OrderByDescending(c => c.ClassId)
                        .FirstOrDefaultAsync();
                    
                    var nextNumber = (lastClass?.ClassId ?? 0) + 1;
                    classModel.Code = $"{course?.Code}-{nextNumber:D2}";
                }

                _context.Classes.Add(classModel);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Tạo lớp học '{classModel.Name}' thành công!";
                return RedirectToAction("ClassManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo lớp học: {ex.Message}. Chi tiết: {ex.InnerException?.Message}";
                
                // Reload dropdown data
                var currentFacultyId = GetCurrentFacultyId();
                var courses = await _context.Courses
                    .Where(c => c.FacultyId == currentFacultyId && c.DeletedAt == null && c.IsActive == true)
                    .ToListAsync();
                
                var teachers = await _context.Users
                    .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 3 && u.DeletedAt == null)
                    .ToListAsync();

                ViewBag.Courses = new SelectList(courses, "CourseId", "Name");
                ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
                ViewBag.CurrentFacultyId = currentFacultyId;

                return View(classModel);
            }
        }

        // GET: Chỉnh sửa lớp học
        public async Task<IActionResult> EditClass(int id)
        {
            if (!CheckFacultyAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var currentFacultyId = GetCurrentFacultyId();
            if (currentFacultyId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                return RedirectToAction("Index", "Home");
            }

            var classModel = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (classModel == null || classModel.DeletedAt != null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("ClassManagement");
            }

            // Load courses của khoa
            var courses = await _context.Courses
                .Where(c => c.FacultyId == currentFacultyId.Value && c.DeletedAt == null && c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // Load giảng viên của khoa
            var teachers = await _context.Users
                .Where(u => u.FacultyId == currentFacultyId.Value && u.RoleId == 3 && u.DeletedAt == null)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // Load danh sách học kỳ đã được cấu hình
            var semesters = await _context.SemesterConfigs
                .Where(s => s.IsActive == true)
                .OrderByDescending(s => s.AcademicYear)
                .ThenBy(s => s.SemesterNumber)
                .ToListAsync();

            // Tìm học kỳ hiện tại dựa trên trường Semester của Class
            int? currentSemesterId = null;
            if (!string.IsNullOrEmpty(classModel.Semester))
            {
                // Tạo pattern tương tự: "Học kỳ 1 - Năm học 2025 - 2026"
                var matchingSemester = semesters.FirstOrDefault(s => 
                    classModel.Semester.Contains($"Học kỳ {s.SemesterNumber}") && 
                    classModel.Semester.Contains(s.AcademicYear));
                
                if (matchingSemester != null)
                {
                    currentSemesterId = matchingSemester.SemesterConfigId;
                }
            }

            ViewBag.Courses = new SelectList(courses, "CourseId", "Name", classModel.CourseId);
            ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName", classModel.InstructorId);
            ViewBag.Semesters = semesters;
            ViewBag.CurrentSemesterId = currentSemesterId;
            ViewBag.CurrentFacultyId = currentFacultyId.Value;

            var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
            ViewBag.FacultyName = faculty?.Name ?? "";
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View(classModel);
        }

        // POST: Chỉnh sửa lớp học
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClass(int id, Class classModel)
        {
            if (id != classModel.ClassId)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ!";
                return RedirectToAction("ClassManagement");
            }

            try
            {
                var existingClass = await _context.Classes.FindAsync(id);
                if (existingClass == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                    return RedirectToAction("ClassManagement");
                }

                // Update fields
                existingClass.Name = classModel.Name;
                existingClass.Code = classModel.Code;
                existingClass.CourseId = classModel.CourseId;
                existingClass.InstructorId = classModel.InstructorId;
                existingClass.MaxStudents = classModel.MaxStudents;
                existingClass.Credits = classModel.Credits;
                existingClass.CourseType = classModel.CourseType;
                existingClass.StartDate = classModel.StartDate;
                existingClass.EndDate = classModel.EndDate;
                existingClass.StartTime = classModel.StartTime;
                existingClass.EndTime = classModel.EndTime;
                existingClass.Semester = classModel.Semester;
                existingClass.Session = classModel.Session;
                existingClass.DayOfWeek = classModel.DayOfWeek;
                existingClass.Description = classModel.Description;
                existingClass.Objectives = classModel.Objectives;
                existingClass.IntroVideoUrl = classModel.IntroVideoUrl;
                existingClass.SlidesUrl = classModel.SlidesUrl;
                existingClass.LectureVideoUrl = classModel.LectureVideoUrl;
                existingClass.MissionTitle = classModel.MissionTitle;
                existingClass.IsActive = classModel.IsActive;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cập nhật lớp '{existingClass.Name}' thành công!";
                return RedirectToAction("ClassManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật: {ex.Message}";
                
                // Reload dropdown data
                var currentFacultyId = GetCurrentFacultyId();
                var courses = await _context.Courses
                    .Where(c => c.FacultyId == currentFacultyId && c.DeletedAt == null && c.IsActive == true)
                    .ToListAsync();
                
                var teachers = await _context.Users
                    .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 3 && u.DeletedAt == null)
                    .ToListAsync();

                ViewBag.Courses = new SelectList(courses, "CourseId", "Name");
                ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
                ViewBag.CurrentFacultyId = currentFacultyId;

                return View(classModel);
            }
        }

        // EXPORT/IMPORT EXCEL
        
        // GET: Export danh sách sinh viên ra Excel
        public async Task<IActionResult> ExportStudentsToExcel(string? administrativeClass = null, int? departmentId = null)
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Lấy FacultyId từ session
                var facultyIdStr = HttpContext.Session.GetString("FacultyId");
                if (string.IsNullOrEmpty(facultyIdStr) || !int.TryParse(facultyIdStr, out var currentFacultyId))
                {
                    TempData["ErrorMessage"] = "Không xác định được khoa của bạn!";
                    return RedirectToAction("StudentManagement");
                }

                // Lấy danh sách sinh viên
                var query = _context.Users
                    .Include(u => u.Faculty)
                    .Include(u => u.Department)
                    .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 4 && u.DeletedAt == null);

                // Lọc theo lớp hành chính nếu có
                if (!string.IsNullOrEmpty(administrativeClass))
                {
                    query = query.Where(u => u.StudentClass == administrativeClass);
                }

                // Lọc theo chuyên ngành nếu có
                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(u => u.DepartmentId == departmentId.Value);
                }

                var students = await query.OrderBy(u => u.MssvMgv).ToListAsync();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Danh sách sinh viên");

                    // Header
                    worksheet.Cells[1, 1].Value = "MSSV";
                    worksheet.Cells[1, 2].Value = "Họ và tên";
                    worksheet.Cells[1, 3].Value = "Email";
                    worksheet.Cells[1, 4].Value = "Số điện thoại";
                    worksheet.Cells[1, 5].Value = "Lớp hành chính";
                    worksheet.Cells[1, 6].Value = "Chuyên ngành";

                    // Style header
                    using (var range = worksheet.Cells[1, 1, 1, 6])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                        range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    }

                    // Data
                    int row = 2;
                    foreach (var student in students)
                    {
                        worksheet.Cells[row, 1].Value = student.MssvMgv;
                        worksheet.Cells[row, 2].Value = student.FullName;
                        worksheet.Cells[row, 3].Value = student.Email;
                        worksheet.Cells[row, 4].Value = student.Phone ?? "";
                        worksheet.Cells[row, 5].Value = student.StudentClass ?? "";
                        worksheet.Cells[row, 6].Value = student.Department?.Name ?? "";
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    // Generate file
                    var fileName = $"DanhSachSinhVien_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    var fileBytes = package.GetAsByteArray();

                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xuất file Excel: {ex.Message}";
                return RedirectToAction("StudentManagement");
            }
        }

        // POST: Import sinh viên vào lớp học từ Excel
        [HttpPost]
        public async Task<IActionResult> ImportStudentsToClass(IFormFile excelFile, int classId)
        {
            try
            {
                if (excelFile == null || excelFile.Length == 0)
                {
                    TempData["ErrorMessage"] = "Vui lòng chọn file Excel!";
                    return RedirectToAction("ClassManagement");
                }

                // Kiểm tra lớp học tồn tại
                var classEntity = await _context.Classes
                    .Include(c => c.Course)
                    .Include(c => c.ClassStudents)
                    .FirstOrDefaultAsync(c => c.ClassId == classId);

                if (classEntity == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                    return RedirectToAction("ClassManagement");
                }

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                int successCount = 0;
                int errorCount = 0;
                var errorMessages = new List<string>();

                using (var stream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension.Rows;

                        // Bắt đầu từ row 2 (bỏ qua header)
                        for (int row = 2; row <= rowCount; row++)
                        {
                            try
                            {
                                var mssv = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                                
                                if (string.IsNullOrEmpty(mssv))
                                    continue;

                                // Tìm sinh viên theo MSSV
                                var student = await _context.Users
                                    .FirstOrDefaultAsync(u => u.MssvMgv == mssv && u.RoleId == 4 && u.DeletedAt == null);

                                if (student == null)
                                {
                                    errorMessages.Add($"Dòng {row}: Không tìm thấy sinh viên có MSSV {mssv}");
                                    errorCount++;
                                    continue;
                                }

                                // Kiểm tra sinh viên đã có trong lớp chưa
                                var existing = await _context.ClassStudents
                                    .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == student.UserId);

                                if (existing != null)
                                {
                                    errorMessages.Add($"Dòng {row}: Sinh viên {mssv} đã có trong lớp");
                                    errorCount++;
                                    continue;
                                }

                                // Kiểm tra sĩ số lớp
                                if (classEntity.MaxStudents.HasValue && 
                                    classEntity.CurrentStudents >= classEntity.MaxStudents)
                                {
                                    errorMessages.Add($"Dòng {row}: Lớp đã đầy (tối đa {classEntity.MaxStudents} sinh viên)");
                                    errorCount++;
                                    continue;
                                }

                                // Thêm sinh viên vào lớp
                                var classStudent = new ClassStudent
                                {
                                    ClassId = classId,
                                    StudentId = student.UserId,
                                    EnrollDate = DateTime.Now,
                                    Status = "Active"
                                };

                                _context.ClassStudents.Add(classStudent);
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                errorMessages.Add($"Dòng {row}: {ex.Message}");
                                errorCount++;
                            }
                        }

                        // Cập nhật số lượng sinh viên sau khi thêm xong
                        if (successCount > 0)
                        {
                            classEntity.CurrentStudents = (classEntity.CurrentStudents ?? 0) + successCount;
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                // Thông báo kết quả
                if (successCount > 0)
                {
                    TempData["SuccessMessage"] = $"Đã thêm {successCount} sinh viên vào lớp {classEntity.Name}!";
                }

                if (errorCount > 0)
                {
                    var errorSummary = string.Join("<br/>", errorMessages.Take(10));
                    if (errorMessages.Count > 10)
                    {
                        errorSummary += $"<br/>... và {errorMessages.Count - 10} lỗi khác";
                    }
                    TempData["ErrorMessage"] = $"Có {errorCount} lỗi:<br/>{errorSummary}";
                }

                return RedirectToAction("ClassManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi import file: {ex.Message}";
                return RedirectToAction("ClassManagement");
            }
        }

        // Đăng xuất Faculty Admin
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}