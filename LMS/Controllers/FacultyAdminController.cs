using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LMS.Controllers
{
    public class FacultyAdminController : Controller
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

            ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
            ViewBag.CurrentFacultyId = currentFacultyId;

            return View();
        }

        // POST: Tạo khóa học mới
        [HttpPost]
        public async Task<IActionResult> CreateCourse(Course course)
        {
            try
            {
                course.CreatedAt = DateTime.Now;
                course.IsActive = true;
                course.DeletedAt = null;

                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo khóa học mới thành công!";
                return RedirectToAction("CourseManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo khóa học: {ex.Message}";
                
                // Reload dropdown data
                var currentFacultyId = course.FacultyId;
                var teachers = await _context.Users
                    .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 3 && u.DeletedAt == null)
                    .ToListAsync();
                ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
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
            var currentFacultyId = 1; // TODO: Lấy từ session

            var students = await _context.Users
                .Include(u => u.Faculty)
                .Include(u => u.Department)
                .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 4 && u.DeletedAt == null)
                .ToListAsync();

            return View(students);
        }

        // GET: Import sinh viên từ Excel
        public IActionResult ImportStudents()
        {
            return View();
        }

        // POST: Import sinh viên từ Excel
        [HttpPost]
        public async Task<IActionResult> ImportStudents(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel!";
                return View();
            }

            try
            {
                // TODO: Implement Excel import logic
                // Có thể sử dụng thư viện EPPlus hoặc ClosedXML để đọc Excel
                
                TempData["SuccessMessage"] = "Import sinh viên thành công! (Chức năng sẽ được hoàn thiện sau)";
                return RedirectToAction("StudentManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi import sinh viên: {ex.Message}";
                return View();
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



        // Đăng xuất Faculty Admin
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Đăng xuất thành công!";
            return RedirectToAction("Index", "Home");
        }
    }
}