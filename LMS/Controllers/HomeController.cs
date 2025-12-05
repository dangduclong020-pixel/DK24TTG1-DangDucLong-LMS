using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;

namespace LMS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly LmsSystemContext _context;

        public HomeController(ILogger<HomeController> logger, LmsSystemContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var faculties = _context.Faculties
                .Where(f => f.IsActive == true && f.DeletedAt == null)
                .OrderBy(f => f.Name)
                .ToList();
            
            // Kiểm tra trạng thái đăng nhập từ session (tạm thời dùng TempData)
            ViewBag.IsLoggedIn = TempData["IsLoggedIn"] as bool? ?? false;
            ViewBag.UserName = TempData["UserName"] as string ?? "";
            ViewBag.UserRole = TempData["UserRole"] as string ?? "";
            
            // Keep data for next request
            TempData.Keep("IsLoggedIn");
            TempData.Keep("UserName");
            TempData.Keep("UserRole");
            
            return View(faculties);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // GET: Login
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false)
        {
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                try
                {
                    // Tìm user trong database với thông tin Faculty
                    var user = await _context.Users
                        .Include(u => u.Role)
                        .Include(u => u.Faculty)
                        .FirstOrDefaultAsync(u => u.MssvMgv == username && u.PasswordHash == password && u.Status == "Active");

                    if (user != null)
                    {
                        // Debug: Log user information before setting session
                        _logger.LogInformation($"User found: {user.FullName}, Role: '{user.Role?.RoleName}', Faculty: '{user.Faculty?.Name}'");
                        
                        // Set trạng thái đăng nhập vào session
                        HttpContext.Session.SetString("IsLoggedIn", "true");
                        HttpContext.Session.SetString("IsLoggedIn", "true");
                        HttpContext.Session.SetString("UserId", user.UserId.ToString());
                        HttpContext.Session.SetString("UserName", user.FullName);
                        HttpContext.Session.SetString("UserRole", user.Role?.RoleName ?? "User");
                        HttpContext.Session.SetString("FacultyId", user.FacultyId?.ToString() ?? "");
                        HttpContext.Session.SetString("FacultyName", user.Faculty?.Name ?? "");
                        
                        // Set TempData for immediate use
                        TempData["IsLoggedIn"] = true;
                        TempData["UserName"] = user.FullName;
                        TempData["UserRole"] = user.Role?.RoleName ?? "User";
                        TempData["FacultyName"] = user.Faculty?.Name ?? "";
                        TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {user.FullName}";
                        
                        // Debug: Log role information
                        var roleName = user.Role?.RoleName?.Trim();
                        _logger.LogInformation($"User {user.FullName} logged in with role: '{roleName}' - preparing redirect");
                        
                        // Chuyển hướng dựa trên role (case insensitive)
                        switch (roleName?.ToLower())
                        {
                            case "admin":
                                return RedirectToAction("Index", "Admin");
                            case "quản trị khoa":
                            case "faculty admin":
                            case "quan tri khoa":
                            case "departmentadmin":
                                return RedirectToAction("Index", "FacultyAdmin");
                            case "giảng viên":
                            case "teacher":
                            case "giang vien":
                                return RedirectToAction("Index", "Teacher");
                            case "sinh viên":
                            case "student":
                            case "sinh vien":
                                return RedirectToAction("Index", "Student");
                            default:
                                _logger.LogWarning($"Unknown role '{roleName}' for user {user.FullName}, redirecting to home");
                                return RedirectToAction("Index");
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Lỗi hệ thống: {ex.Message}";
                    return View();
                }
            }
            
            TempData["ErrorMessage"] = "Tên đăng nhập hoặc mật khẩu không đúng!";
            return View();
        }

        // Test action to debug users and roles
        public async Task<IActionResult> TestUsers()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Faculty)
                .Where(u => u.Status == "Active")
                .ToListAsync();
            
            return View(users);
        }

        // Action to create test data if needed
        public async Task<IActionResult> CreateTestData()
        {
            try
            {
                // Check if we have faculty admin role
                var facultyAdminRole = await _context.Roles
                    .FirstOrDefaultAsync(r => r.RoleName.Contains("Quản trị") || r.RoleName.Contains("Faculty"));
                
                if (facultyAdminRole == null)
                {
                    // Create Faculty Admin role
                    facultyAdminRole = new Role 
                    { 
                        RoleName = "Quản trị Khoa",
                        Description = "Quản trị viên cấp khoa"
                    };
                    _context.Roles.Add(facultyAdminRole);
                    await _context.SaveChangesAsync();
                }

                // Check if we have Faculty
                var faculty = await _context.Faculties.FirstOrDefaultAsync();
                if (faculty == null)
                {
                    faculty = new Faculty
                    {
                        Name = "Khoa Ngoại Ngữ",
                        Code = "FL",
                        IsActive = true
                    };
                    _context.Faculties.Add(faculty);
                    await _context.SaveChangesAsync();
                }

                // Check if test user exists
                var testUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.MssvMgv == "admin");
                    
                if (testUser == null)
                {
                    testUser = new User
                    {
                        MssvMgv = "admin",
                        PasswordHash = "123456", // Simple password for testing
                        FullName = "Nguyễn Ngoại Ngữ",
                        Email = "admin@faculty.edu.vn",
                        RoleId = facultyAdminRole.RoleId,
                        FacultyId = faculty.FacultyId,
                        Status = "Active"
                    };
                    _context.Users.Add(testUser);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Test data created successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult Logout()
        {
            // Xóa hoàn toàn session đăng nhập
            TempData.Remove("IsLoggedIn");
            TempData.Remove("UserName");
            TempData.Remove("UserRole");
            TempData.Remove("FacultyName");
            
            // Xóa tất cả TempData
            TempData.Clear();
            
            // Xóa session nếu có
            HttpContext.Session.Clear();
            
            TempData["SuccessMessage"] = "Đã đăng xuất thành công!";
            
            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
