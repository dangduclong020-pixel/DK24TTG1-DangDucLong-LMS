using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OfficeOpenXml;
using System.Text;

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

        // POST: Auto assign departments for students without department
        [HttpPost]
        public async Task<IActionResult> AutoAssignDepartments()
        {
            try
            {
                // Lấy FacultyId từ session
                var facultyIdStr = HttpContext.Session.GetString("FacultyId");
                if (!string.IsNullOrEmpty(facultyIdStr) && int.TryParse(facultyIdStr, out var currentFacultyId))
                {
                    // Lấy danh sách sinh viên chưa có chuyên ngành
                    var studentsWithoutDept = await _context.Users
                        .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 4 && u.DepartmentId == null && u.DeletedAt == null)
                        .ToListAsync();

                    // Lấy danh sách chuyên ngành của khoa
                    var departments = await _context.Departments
                        .Where(d => d.FacultyId == currentFacultyId && d.IsActive == true && d.DeletedAt == null)
                        .ToListAsync();

                    if (departments.Count > 0 && studentsWithoutDept.Count > 0)
                    {
                        // Phân đều sinh viên vào các chuyên ngành
                        for (int i = 0; i < studentsWithoutDept.Count; i++)
                        {
                            var deptIndex = i % departments.Count;
                            studentsWithoutDept[i].DepartmentId = departments[deptIndex].DepartmentId;
                            studentsWithoutDept[i].UpdatedAt = DateTime.Now;
                        }

                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = $"Đã tự động phân {studentsWithoutDept.Count} sinh viên vào {departments.Count} chuyên ngành!";
                    }
                    else
                    {
                        TempData["WarningMessage"] = "Không có sinh viên nào cần phân chuyên ngành hoặc không có chuyên ngành khả dụng!";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tự động phân chuyên ngành: {ex.Message}";
            }

            return RedirectToAction("StudentManagement");
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

            ViewBag.Courses = new SelectList(courses, "CourseId", "Name");
            ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
            ViewBag.CurrentFacultyId = currentFacultyId.Value;

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

        // =====================================================
        // QUẢN LÝ LỚP HÀNH CHÍNH
        // =====================================================

        // GET: Danh sách lớp hành chính
        public async Task<IActionResult> AdministrativeClassManagement()
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

            var administrativeClasses = await _context.AdministrativeClasses
                .Include(ac => ac.Faculty)
                .Include(ac => ac.Department)
                .Include(ac => ac.Advisor)
                .Where(ac => ac.FacultyId == currentFacultyId.Value && ac.DeletedAt == null)
                .OrderByDescending(ac => ac.AcademicYear)
                .ThenBy(ac => ac.Code)
                .ToListAsync();

            var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
            ViewBag.FacultyName = faculty?.Name ?? "";
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View(administrativeClasses);
        }

        // GET: Tạo lớp hành chính mới
        public async Task<IActionResult> CreateAdministrativeClass()
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

            // Lấy danh sách bộ môn của khoa
            var departments = await _context.Departments
                .Where(d => d.FacultyId == currentFacultyId.Value && d.IsActive == true && d.DeletedAt == null)
                .ToListAsync();

            // Lấy danh sách giảng viên của khoa để chọn GVCN
            var teachers = await _context.Users
                .Where(u => u.FacultyId == currentFacultyId.Value && u.RoleId == 3 && u.DeletedAt == null)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Departments = new SelectList(departments, "DepartmentId", "Name");
            ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
            ViewBag.CurrentFacultyId = currentFacultyId.Value;

            var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
            ViewBag.FacultyName = faculty?.Name ?? "";
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View();
        }

        // POST: Tạo lớp hành chính mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdministrativeClass(AdministrativeClass administrativeClass)
        {
            try
            {
                var currentFacultyId = GetCurrentFacultyId();
                var currentUserId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

                // Set required fields
                administrativeClass.FacultyId = currentFacultyId.Value;
                administrativeClass.CreatedAt = DateTime.Now;
                administrativeClass.CreatedBy = currentUserId;
                administrativeClass.IsActive = administrativeClass.IsActive ?? true;
                administrativeClass.MaxStudents = administrativeClass.MaxStudents ?? 40;
                administrativeClass.CurrentStudents = 0;
                administrativeClass.DeletedAt = null;

                // Auto-generate code if empty
                if (string.IsNullOrWhiteSpace(administrativeClass.Code))
                {
                    var faculty = await _context.Faculties.FindAsync(currentFacultyId.Value);
                    var year = administrativeClass.AcademicYear?.Split('-')[0] ?? DateTime.Now.Year.ToString();
                    var lastTwoDigits = year.Length >= 2 ? year.Substring(year.Length - 2) : year;
                    
                    var count = await _context.AdministrativeClasses
                        .Where(ac => ac.FacultyId == currentFacultyId.Value && ac.AcademicYear == administrativeClass.AcademicYear)
                        .CountAsync();
                    
                    var letter = (char)('A' + count);
                    administrativeClass.Code = $"{faculty?.Code}{lastTwoDigits}{letter}";
                }

                _context.AdministrativeClasses.Add(administrativeClass);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Tạo lớp hành chính '{administrativeClass.Name}' ({administrativeClass.Code}) thành công!";
                return RedirectToAction("AdministrativeClassManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo lớp hành chính: {ex.Message}";
                
                // Reload dropdown data
                var currentFacultyId = GetCurrentFacultyId();
                var departments = await _context.Departments
                    .Where(d => d.FacultyId == currentFacultyId && d.IsActive == true && d.DeletedAt == null)
                    .ToListAsync();
                
                var teachers = await _context.Users
                    .Where(u => u.FacultyId == currentFacultyId && u.RoleId == 3 && u.DeletedAt == null)
                    .ToListAsync();

                ViewBag.Departments = new SelectList(departments, "DepartmentId", "Name");
                ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName");
                ViewBag.CurrentFacultyId = currentFacultyId;

                return View(administrativeClass);
            }
        }

        // GET: Chỉnh sửa lớp hành chính
        public async Task<IActionResult> EditAdministrativeClass(int id)
        {
            if (!CheckFacultyAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var administrativeClass = await _context.AdministrativeClasses
                .Include(ac => ac.Faculty)
                .Include(ac => ac.Department)
                .Include(ac => ac.Advisor)
                .FirstOrDefaultAsync(ac => ac.AdministrativeClassId == id);

            if (administrativeClass == null || administrativeClass.DeletedAt != null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp hành chính!";
                return RedirectToAction("AdministrativeClassManagement");
            }

            // Lấy danh sách bộ môn và giảng viên
            var departments = await _context.Departments
                .Where(d => d.FacultyId == administrativeClass.FacultyId && d.IsActive == true && d.DeletedAt == null)
                .ToListAsync();
            
            var teachers = await _context.Users
                .Where(u => u.FacultyId == administrativeClass.FacultyId && u.RoleId == 3 && u.DeletedAt == null)
                .ToListAsync();

            ViewBag.Departments = new SelectList(departments, "DepartmentId", "Name", administrativeClass.DepartmentId);
            ViewBag.Teachers = new SelectList(teachers, "UserId", "FullName", administrativeClass.AdvisorId);
            ViewBag.FacultyName = administrativeClass.Faculty.Name;
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View(administrativeClass);
        }

        // POST: Chỉnh sửa lớp hành chính
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAdministrativeClass(int id, AdministrativeClass administrativeClass)
        {
            if (id != administrativeClass.AdministrativeClassId)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ!";
                return RedirectToAction("AdministrativeClassManagement");
            }

            try
            {
                var existingClass = await _context.AdministrativeClasses.FindAsync(id);
                if (existingClass == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy lớp hành chính!";
                    return RedirectToAction("AdministrativeClassManagement");
                }

                // Update fields
                existingClass.Name = administrativeClass.Name;
                existingClass.Code = administrativeClass.Code;
                existingClass.DepartmentId = administrativeClass.DepartmentId;
                existingClass.AcademicYear = administrativeClass.AcademicYear;
                existingClass.Intake = administrativeClass.Intake;
                existingClass.MaxStudents = administrativeClass.MaxStudents;
                existingClass.AdvisorId = administrativeClass.AdvisorId;
                existingClass.IsActive = administrativeClass.IsActive;
                existingClass.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cập nhật lớp '{existingClass.Name}' thành công!";
                return RedirectToAction("AdministrativeClassManagement");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật: {ex.Message}";
                return RedirectToAction("EditAdministrativeClass", new { id });
            }
        }

        // POST: Xóa lớp hành chính (soft delete)
        [HttpPost]
        public async Task<IActionResult> DeleteAdministrativeClass(int id)
        {
            try
            {
                var administrativeClass = await _context.AdministrativeClasses.FindAsync(id);
                if (administrativeClass == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp hành chính!" });
                }

                // Kiểm tra có sinh viên không
                var studentCount = await _context.Users
                    .CountAsync(u => u.AdministrativeClassId == id && u.DeletedAt == null);

                if (studentCount > 0)
                {
                    return Json(new { success = false, message = $"Không thể xóa! Lớp còn {studentCount} sinh viên." });
                }

                // Soft delete
                administrativeClass.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Đã xóa lớp '{administrativeClass.Name}' thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Danh sách sinh viên trong lớp hành chính
        public async Task<IActionResult> AdministrativeClassStudents(int id)
        {
            if (!CheckFacultyAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var administrativeClass = await _context.AdministrativeClasses
                .Include(ac => ac.Faculty)
                .Include(ac => ac.Advisor)
                .FirstOrDefaultAsync(ac => ac.AdministrativeClassId == id);

            if (administrativeClass == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp hành chính!";
                return RedirectToAction("AdministrativeClassManagement");
            }

            var students = await _context.Users
                .Where(u => u.AdministrativeClassId == id && u.RoleId == 4 && u.DeletedAt == null)
                .OrderBy(u => u.MssvMgv)
                .ToListAsync();

            ViewBag.AdministrativeClass = administrativeClass;
            ViewBag.FacultyName = administrativeClass.Faculty.Name;
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View(students);
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