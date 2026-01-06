using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;

namespace LMS.Controllers
{
    public class HomeController : BaseController
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
            
            // ViewBag đã được set tự động bởi BaseController
            
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
                        HttpContext.Session.SetInt32("UserId", user.UserId);
                        HttpContext.Session.SetString("UserName", user.FullName);
                        HttpContext.Session.SetString("UserRole", user.Role?.RoleName ?? "User");
                        HttpContext.Session.SetString("RoleId", user.RoleId?.ToString() ?? "");
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
                                // Giảng viên đăng nhập xong quay về trang home
                                return RedirectToAction("Index", "Home");
                            case "sinh viên":
                            case "student":
                            case "sinh vien":
                                // Sinh viên đăng nhập xong quay về trang home
                                return RedirectToAction("Index", "Home");
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

        // Debug action to check all roles
        public async Task<IActionResult> DebugRoles()
        {
            var roles = await _context.Roles.ToListAsync();
            var users = await _context.Users.Include(u => u.Role).ToListAsync();
            
            ViewBag.Roles = roles;
            ViewBag.Users = users;
            
            return Content($"Roles: {string.Join(", ", roles.Select(r => $"ID:{r.RoleId} Name:'{r.RoleName}'"))}\n\n" +
                          $"Users: {string.Join("\n", users.Select(u => $"{u.FullName} - Role: {u.Role?.RoleName} (ID: {u.RoleId})"))}");
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

        // Trang tổng quan khóa học cho sinh viên
        public async Task<IActionResult> MyCourses()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn") == "true";
            if (!isLoggedIn)
            {
                return RedirectToAction("Login");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Lấy các lớp học mà sinh viên đã tham gia
            var studentClasses = await _context.ClassStudents
                .Include(cs => cs.Class)
                    .ThenInclude(c => c.Course)
                        .ThenInclude(co => co.Faculty)
                .Include(cs => cs.Class)
                    .ThenInclude(c => c.Assignments)
                        .ThenInclude(a => a.Submissions.Where(s => s.StudentId == userId))
                .Include(cs => cs.Class)
                    .ThenInclude(c => c.Exams)
                        .ThenInclude(e => e.ExamResults.Where(er => er.StudentId == userId))
                .Where(cs => cs.StudentId == userId && (cs.Status == "Approved" || cs.Status == "Active"))
                .OrderByDescending(cs => cs.EnrollDate)
                .ToListAsync();

            ViewBag.IsLoggedIn = isLoggedIn;
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "";

            return View(studentClasses);
        }

        // Trang chi tiết khóa học
        public async Task<IActionResult> CourseDetail(int id)
        {
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn") == "true";
            if (!isLoggedIn)
            {
                return RedirectToAction("Login");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Lấy thông tin lớp học chi tiết
            var classDetail = await _context.Classes
                .Include(c => c.Course)
                    .ThenInclude(co => co.Faculty)
                .Include(c => c.Instructor)
                .Include(c => c.ClassStudents)
                .Include(c => c.Assignments)
                    .ThenInclude(a => a.Submissions.Where(s => s.StudentId == userId))
                .Include(c => c.Exams.Where(e => e.DeletedAt == null))
                    .ThenInclude(e => e.ExamResults.Where(er => er.StudentId == userId))
                .Include(c => c.Lessons.Where(l => l.DeletedAt == null))
                    .ThenInclude(l => l.LessonFiles)
                .Include(c => c.Attendances)
                    .ThenInclude(a => a.AttendanceRecords)
                .Include(c => c.Attendances)
                    .ThenInclude(a => a.Lesson)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (classDetail == null)
            {
                return NotFound();
            }

            // Kiểm tra xem sinh viên có trong lớp này không
            var isEnrolled = await _context.ClassStudents
                .AnyAsync(cs => cs.ClassId == id && cs.StudentId == userId && (cs.Status == "Approved" || cs.Status == "Active"));

            if (!isEnrolled)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập vào lớp học này!";
                return RedirectToAction("MyCourses");
            }

            ViewBag.IsLoggedIn = isLoggedIn;
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "";

            return View(classDetail);
        }

        public async Task<IActionResult> AssignmentDetail(int id)
        {
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn") == "true";
            if (!isLoggedIn)
            {
                return RedirectToAction("Login");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Kiểm tra role của người dùng
            var roleId = HttpContext.Session.GetString("RoleId");
            var userRole = HttpContext.Session.GetString("UserRole");
            
            // Nếu là giảng viên, redirect đến trang tổng quan chấm điểm
            if (roleId == "3" || userRole == "Giảng viên" || userRole == "Teacher" || userRole?.ToLower() == "giang vien")
            {
                return RedirectToAction("GradingOverviewAssignment", "Teacher", new { id });
            }

            var assignment = await _context.Assignments
                .Include(a => a.Class)
                .Include(a => a.Submissions.Where(s => s.StudentId == userId))
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null)
            {
                return NotFound();
            }

            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAssignment(int assignmentId, IFormFile file)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var assignment = await _context.Assignments.FindAsync(assignmentId);
            if (assignment == null)
            {
                return NotFound();
            }

            try
            {
                string filePath = null;
                string fileName = null;

                if (file != null && file.Length > 0)
                {
                    // Tạo thư mục uploads nếu chưa có
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "submissions");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Tạo tên file unique
                    fileName = $"{userId}_{assignmentId}_{DateTime.Now.Ticks}{Path.GetExtension(file.FileName)}";
                    filePath = Path.Combine(uploadsFolder, fileName);

                    // Lưu file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Đường dẫn relative để lưu vào DB
                    filePath = $"/uploads/submissions/{fileName}";
                }

                // Kiểm tra xem đã nộp bài chưa
                var existingSubmission = await _context.Submissions
                    .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == userId);

                if (existingSubmission != null)
                {
                    // Cập nhật bài nộp cũ
                    existingSubmission.FilePath = filePath ?? existingSubmission.FilePath;
                    existingSubmission.FileName = fileName ?? existingSubmission.FileName;
                    existingSubmission.SubmittedAt = DateTime.Now;
                }
                else
                {
                    // Tạo bài nộp mới
                    var submission = new Submission
                    {
                        AssignmentId = assignmentId,
                        StudentId = userId.Value,
                        FilePath = filePath,
                        FileName = fileName ?? file?.FileName,
                        SubmittedAt = DateTime.Now
                    };
                    _context.Submissions.Add(submission);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Nộp bài thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi nộp bài: " + ex.Message;
            }

            return RedirectToAction("AssignmentDetail", new { id = assignmentId });
        }

        // GET: ExamIntro - Trang giới thiệu bài kiểm tra
        public async Task<IActionResult> ExamIntro(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Kiểm tra role của người dùng
            var roleId = HttpContext.Session.GetString("RoleId");
            var userRole = HttpContext.Session.GetString("UserRole");
            
            // Nếu là giảng viên, redirect đến trang tổng quan chấm điểm
            if (roleId == "3" || userRole == "Giảng viên" || userRole == "Teacher" || userRole?.ToLower() == "giang vien")
            {
                return RedirectToAction("GradingOverviewExam", "Teacher", new { id });
            }

            var exam = await _context.Exams
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.ExamResults.Where(er => er.StudentId == userId))
                .FirstOrDefaultAsync(e => e.ExamId == id && e.DeletedAt == null);

            if (exam == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài kiểm tra!";
                return RedirectToAction("MyCourses");
            }

            return View(exam);
        }

        // GET: StartExam - Bắt đầu làm bài kiểm tra (action mới thay thế ExamDetail)
        public async Task<IActionResult> StartExam(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var exam = await _context.Exams
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                        .ThenInclude(q => q.Difficulty)
                .Include(e => e.ExamResults.Where(er => er.StudentId == userId))
                .FirstOrDefaultAsync(e => e.ExamId == id && e.DeletedAt == null);

            if (exam == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài kiểm tra!";
                return RedirectToAction("MyCourses");
            }

            // Kiểm tra thời gian làm bài
            var now = DateTime.Now;
            if (now < exam.StartTime)
            {
                TempData["ErrorMessage"] = "Bài kiểm tra chưa bắt đầu!";
                return RedirectToAction("ExamIntro", new { id = exam.ExamId });
            }

            if (now > exam.EndTime)
            {
                TempData["ErrorMessage"] = "Bài kiểm tra đã kết thúc!";
                return RedirectToAction("ExamIntro", new { id = exam.ExamId });
            }

            // Kiểm tra số lần đã làm
            var attemptCount = exam.ExamResults?.Count() ?? 0;
            
            // Xác định số lần được phép làm
            int maxAttempts;
            if (exam.MaxAttempts.HasValue && exam.MaxAttempts.Value > 0)
            {
                maxAttempts = exam.MaxAttempts.Value;
            }
            else if (exam.AllowMultipleAttempts == true)
            {
                maxAttempts = -1; // Không giới hạn
            }
            else
            {
                maxAttempts = 1; // Mặc định chỉ 1 lần
            }
            
            // Chỉ kiểm tra nếu có giới hạn
            if (maxAttempts > 0 && attemptCount >= maxAttempts)
            {
                TempData["ErrorMessage"] = "Bạn đã hết lượt làm bài kiểm tra này!";
                return RedirectToAction("ExamIntro", new { id = exam.ExamId });
            }

            return View("ExamDetail", exam);
        }

        // GET: ExamDetail - Giữ lại để tương thích ngược, redirect đến ExamIntro
        public async Task<IActionResult> ExamDetail(int id)
        {
            return RedirectToAction("ExamIntro", new { id });
        }

        // POST: SaveExamProgress - Lưu tạm câu trả lời
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveExamProgress(int examId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Lưu câu trả lời vào session
            var answers = new Dictionary<int, string>();
            foreach (var key in Request.Form.Keys)
            {
                if (key.StartsWith("question_"))
                {
                    var questionId = int.Parse(key.Replace("question_", ""));
                    var answer = Request.Form[key].ToString();
                    if (!string.IsNullOrEmpty(answer))
                    {
                        answers[questionId] = answer;
                    }
                }
            }

            HttpContext.Session.SetString($"ExamAnswers_{examId}", System.Text.Json.JsonSerializer.Serialize(answers));

            return RedirectToAction("ExamReview", new { id = examId });
        }

        // GET: ExamReview - Xem lại câu trả lời trước khi nộp
        public async Task<IActionResult> ExamReview(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var exam = await _context.Exams
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.DeletedAt == null);

            if (exam == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài kiểm tra!";
                return RedirectToAction("MyCourses");
            }

            // Lấy câu trả lời từ session
            var answersJson = HttpContext.Session.GetString($"ExamAnswers_{id}");
            var answers = string.IsNullOrEmpty(answersJson) 
                ? new Dictionary<int, string>() 
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(answersJson);

            ViewBag.Answers = answers;
            ViewBag.TimeRemaining = (exam.EndTime - DateTime.Now).TotalSeconds;

            return View(exam);
        }

        // POST: SubmitExam - Nộp bài kiểm tra
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitExam(int examId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var exam = await _context.Exams
                .Include(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            if (exam == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài kiểm tra!";
                return RedirectToAction("MyCourses");
            }

            // Lấy câu trả lời từ session hoặc form
            Dictionary<int, string> answers;
            var answersJson = HttpContext.Session.GetString($"ExamAnswers_{examId}");
            
            if (!string.IsNullOrEmpty(answersJson))
            {
                answers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(answersJson);
            }
            else
            {
                answers = new Dictionary<int, string>();
                foreach (var key in Request.Form.Keys)
                {
                    if (key.StartsWith("question_"))
                    {
                        var questionId = int.Parse(key.Replace("question_", ""));
                        var answer = Request.Form[key].ToString();
                        if (!string.IsNullOrEmpty(answer))
                        {
                            answers[questionId] = answer;
                        }
                    }
                }
            }

            try
            {
                // Tạo ExamResult trước
                var examResult = new ExamResult
                {
                    ExamId = examId,
                    StudentId = userId.Value,
                    Score = 0, // Sẽ update sau
                    TotalScore = exam.MaxScore,
                    Status = "Completed",
                    CompletedAt = DateTime.Now,
                    AttemptNumber = 1
                };

                _context.ExamResults.Add(examResult);
                await _context.SaveChangesAsync(); // Save để có ResultId

                // Tính điểm và tạo ExamAnswer
                decimal totalScore = 0;
                int correctAnswers = 0;

                foreach (var examQuestion in exam.ExamQuestions)
                {
                    var question = examQuestion.Question;
                    var questionId = question.QuestionId;

                    if (answers.ContainsKey(questionId))
                    {
                        var studentAnswer = answers[questionId];

                        // Tạo ExamAnswer với ResultId đã có
                        var examAnswer = new ExamAnswer
                        {
                            ResultId = examResult.ResultId,
                            QuestionId = questionId,
                            StudentAnswer = studentAnswer,
                            IsCorrect = studentAnswer == question.Answer && question.Type == "TracNghiem",
                            AnswerTime = DateTime.Now
                        };

                        _context.ExamAnswers.Add(examAnswer);

                        if (examAnswer.IsCorrect == true)
                        {
                            correctAnswers++;
                            totalScore += examQuestion.QuestionScore ?? 0;
                        }
                    }
                }

                // Cập nhật điểm cho ExamResult
                examResult.Score = totalScore;
                await _context.SaveChangesAsync();

                // Xóa câu trả lời khỏi session
                HttpContext.Session.Remove($"ExamAnswers_{examId}");

                TempData["SuccessMessage"] = $"Nộp bài thành công! Điểm: {totalScore}/{exam.MaxScore}";
                return RedirectToAction("ExamResult", new { id = examResult.ResultId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi nộp bài: " + ex.Message;
                return RedirectToAction("ExamDetail", new { id = examId });
            }
        }

        // GET: ExamResult - Xem kết quả bài kiểm tra
        public async Task<IActionResult> ExamResult(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Log để debug
            _logger.LogInformation($"Looking for ExamResult: ResultId={id}, UserId={userId}");

            var result = await _context.ExamResults
                .Include(er => er.Exam)
                    .ThenInclude(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(er => er.Exam)
                    .ThenInclude(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                .Include(er => er.Student)
                .Include(er => er.ExamAnswers)
                    .ThenInclude(ea => ea.Question)
                .FirstOrDefaultAsync(er => er.ResultId == id);

            if (result == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy kết quả!";
                return RedirectToAction("MyCourses");
            }

            // Kiểm tra quyền truy cập
            if (result.StudentId != userId.Value)
            {
                _logger.LogWarning($"User {userId} trying to access result of user {result.StudentId}");
                TempData["ErrorMessage"] = "Bạn không có quyền xem kết quả này!";
                return RedirectToAction("MyCourses");
            }

            return View(result);
        }

        public IActionResult Logout()
        {
            // Xóa session đăng nhập
            HttpContext.Session.Clear();
            
            // Xóa tất cả TempData
            TempData.Clear();
            
            return RedirectToAction("Index");
        }

        // Sinh viên quét QR code để điểm danh
        public async Task<IActionResult> ScanQRAttendance(int id)
        {
            var attendance = await _context.Attendances
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Class.Instructor)
                .FirstOrDefaultAsync(a => a.AttendanceId == id);

            if (attendance == null)
            {
                return NotFound("Không tìm thấy phiên điểm danh!");
            }

            return View(attendance);
        }

        // Submit QR attendance
        [HttpPost]
        public async Task<IActionResult> SubmitQRAttendance(int attendanceId)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            try
            {
                // Tìm phiên điểm danh
                var attendance = await _context.Attendances
                    .Include(a => a.AttendanceRecords)
                    .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId);

                if (attendance == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phiên điểm danh!" });
                }

                // Kiểm tra phiên điểm danh còn mở không
                if (attendance.Status != "Open")
                {
                    return Json(new { success = false, message = "Phiên điểm danh đã đóng!" });
                }

                // Kiểm tra thời gian
                var now = DateTime.Now;
                if (now < attendance.StartTime)
                {
                    return Json(new { success = false, message = "Phiên điểm danh chưa bắt đầu!" });
                }

                if (now > attendance.EndTime)
                {
                    return Json(new { success = false, message = "Phiên điểm danh đã kết thúc!" });
                }

                // Tìm bản ghi điểm danh của sinh viên
                var record = attendance.AttendanceRecords
                    .FirstOrDefault(r => r.StudentId == userId);

                if (record == null)
                {
                    return Json(new { success = false, message = "Bạn không thuộc lớp học này!" });
                }

                // Kiểm tra đã điểm danh chưa
                if (record.Status == "Present")
                {
                    return Json(new { 
                        success = false, 
                        message = $"Bạn đã điểm danh lúc {record.CheckInTime?.ToString("HH:mm:ss")}!" 
                    });
                }

                // Cập nhật trạng thái
                record.Status = "Present";
                record.CheckInTime = DateTime.Now;
                record.CheckInMethod = "QR Code";
                record.Note = "Tự điểm danh bằng QR";
                record.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Điểm danh thành công!",
                    checkInTime = record.CheckInTime?.ToString("HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitAttendanceCode([FromBody] AttendanceCodeSubmission submission)
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập!" });
                }

                int userId = int.Parse(userIdStr);

                // Tìm phiên điểm danh
                var attendance = await _context.Attendances
                    .Include(a => a.AttendanceRecords)
                    .Include(a => a.Class)
                        .ThenInclude(c => c.ClassStudents)
                    .FirstOrDefaultAsync(a => a.AttendanceId == submission.AttendanceId);

                if (attendance == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phiên điểm danh!" });
                }

                // Kiểm tra phiên điểm danh còn mở không
                if (attendance.Status != "Open")
                {
                    return Json(new { success = false, message = "Phiên điểm danh đã đóng!" });
                }

                // Kiểm tra thời gian
                var now = DateTime.Now;
                if (now < attendance.StartTime)
                {
                    return Json(new { success = false, message = "Phiên điểm danh chưa bắt đầu!" });
                }

                if (now > attendance.EndTime)
                {
                    return Json(new { success = false, message = "Phiên điểm danh đã kết thúc!" });
                }

                // Kiểm tra mã điểm danh
                if (!attendance.AllowCodeAttendance)
                {
                    return Json(new { success = false, message = "Phiên điểm danh này không cho phép dùng mã!" });
                }

                if (string.IsNullOrEmpty(attendance.AttendanceCode) || 
                    attendance.AttendanceCode.Trim().ToUpper() != submission.Code.Trim().ToUpper())
                {
                    return Json(new { success = false, message = "Mã điểm danh không chính xác!" });
                }

                // Kiểm tra sinh viên có trong lớp không
                var isInClass = attendance.Class?.ClassStudents?.Any(cs => cs.StudentId == userId) ?? false;
                if (!isInClass)
                {
                    return Json(new { success = false, message = "Bạn không thuộc lớp học này!" });
                }

                // Kiểm tra đã điểm danh chưa
                var existingRecord = attendance.AttendanceRecords
                    .FirstOrDefault(ar => ar.StudentId == userId);

                if (existingRecord != null)
                {
                    if (existingRecord.Status == "Present")
                    {
                        return Json(new { success = false, message = "Bạn đã điểm danh rồi!" });
                    }
                    else
                    {
                        // Cập nhật nếu trước đó là Absent
                        existingRecord.Status = "Present";
                        existingRecord.CheckInTime = now;
                        existingRecord.CheckInMethod = "Mã code";
                        existingRecord.Note = "Tự điểm danh bằng mã";
                    }
                }
                else
                {
                    // Tạo bản ghi mới
                    var newRecord = new AttendanceRecord
                    {
                        AttendanceId = attendance.AttendanceId,
                        StudentId = userId,
                        Status = "Present",
                        CheckInTime = now,
                        CheckInMethod = "Mã code",
                        Note = "Tự điểm danh bằng mã"
                    };
                    _context.AttendanceRecords.Add(newRecord);
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Điểm danh thành công!",
                    checkInTime = now.ToString("dd/MM/yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        // Thời khóa biểu của sinh viên
        public async Task<IActionResult> MySchedule(int? semesterId, int? weekNumber)
        {
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn") == "true";
            if (!isLoggedIn)
            {
                return RedirectToAction("Login");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Lấy danh sách học kỳ
            var semesters = await _context.SemesterConfigs
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            // Lấy học kỳ hiện tại (đang active) hoặc học kỳ được chọn
            var currentSemester = semesterId.HasValue 
                ? semesters.FirstOrDefault(s => s.SemesterConfigId == semesterId.Value)
                : semesters.FirstOrDefault(s => s.IsActive) ?? semesters.FirstOrDefault();

            // Tính toán danh sách tuần dựa trên học kỳ
            var weeks = new List<dynamic>();
            if (currentSemester != null)
            {
                var startDate = currentSemester.StartDate;
                var endDate = currentSemester.EndDate;
                var currentDate = startDate;
                var weekNum = 1;

                while (currentDate <= endDate)
                {
                    var weekStart = currentDate;
                    var weekEnd = currentDate.AddDays(6);
                    if (weekEnd > endDate) weekEnd = endDate;

                    weeks.Add(new
                    {
                        WeekNumber = weekNum,
                        WeekStart = weekStart,
                        WeekEnd = weekEnd,
                        DisplayText = $"Tuần {weekNum} [từ ngày {weekStart:dd/MM/yyyy} đến ngày {weekEnd:dd/MM/yyyy}]"
                    });

                    currentDate = currentDate.AddDays(7);
                    weekNum++;
                }
            }

            // Lấy tuần hiện tại hoặc tuần được chọn
            var currentWeekNumber = weekNumber ?? 1;
            var selectedWeek = weeks.FirstOrDefault(w => w.WeekNumber == currentWeekNumber) ?? weeks.FirstOrDefault();

            // Tính toán ngày cụ thể cho từng ngày trong tuần
            var weekDates = new List<DateTime>();
            if (selectedWeek != null)
            {
                DateTime weekStartDate = selectedWeek.WeekStart;
                // Đảm bảo bắt đầu từ thứ 2
                while (weekStartDate.DayOfWeek != DayOfWeek.Monday)
                {
                    weekStartDate = weekStartDate.AddDays(-1);
                }

                for (int i = 0; i < 7; i++)
                {
                    weekDates.Add(weekStartDate.AddDays(i));
                }
            }

            // Tạo chuỗi semester để lọc
            var semesterFilter = currentSemester != null 
                ? $"Học kỳ {currentSemester.SemesterNumber} - Năm học {currentSemester.AcademicYear}"
                : null;

            // Lấy tất cả lớp học mà sinh viên đang tham gia
            var studentClassesQuery = _context.ClassStudents
                .Include(cs => cs.Class)
                    .ThenInclude(c => c.Course)
                .Include(cs => cs.Class)
                    .ThenInclude(c => c.Instructor)
                .Include(cs => cs.Class)
                    .ThenInclude(c => c.Schedules)
                        .ThenInclude(s => s.Lesson)
                .Where(cs => cs.StudentId == userId && 
                       (cs.Status == "Approved" || cs.Status == "Active") &&
                       cs.Class.DeletedAt == null &&
                       cs.Class.IsActive == true);

            if (!string.IsNullOrEmpty(semesterFilter))
            {
                studentClassesQuery = studentClassesQuery.Where(cs => cs.Class.Semester == semesterFilter);
            }

            var studentClasses = await studentClassesQuery.ToListAsync();

            // Lấy khoảng thời gian của tuần hiện tại để lọc lịch học
            DateTime? weekStartFilter = null;
            DateTime? weekEndFilter = null;
            if (selectedWeek != null)
            {
                weekStartFilter = selectedWeek.WeekStart;
                weekEndFilter = selectedWeek.WeekEnd;
            }

            // Hàm tính số tiết từ khoảng thời gian
            int CalculateSessionsFromTime(TimeOnly startTime, TimeOnly endTime)
            {
                var totalMinutes = (endTime.Hour * 60 + endTime.Minute) - (startTime.Hour * 60 + startTime.Minute);
                return (int)Math.Ceiling(totalMinutes / 50.0);
            }

            // Tạo dictionary để nhóm theo ngày trong tuần
            var scheduleByDay = new Dictionary<string, List<dynamic>>();
            var daysOfWeek = new[] { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật" };
            
            foreach (var day in daysOfWeek)
            {
                scheduleByDay[day] = new List<dynamic>();
            }

            foreach (var cs in studentClasses)
            {
                var classInfo = cs.Class;
                
                // Ưu tiên lấy từ bảng Schedules trước
                if (classInfo.Schedules != null && classInfo.Schedules.Any())
                {
                    foreach (var schedule in classInfo.Schedules)
                    {
                        // Kiểm tra xem lớp học có diễn ra trong tuần này không
                        if (weekStartFilter.HasValue && weekEndFilter.HasValue && classInfo.StartDate.HasValue)
                        {
                            var classStartDate = classInfo.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                            
                            // Tính ngày kết thúc thực tế
                            DateTime calculatedEndDate;
                            if (classInfo.EndDate.HasValue)
                            {
                                calculatedEndDate = classInfo.EndDate.Value.ToDateTime(TimeOnly.MinValue);
                            }
                            else if (classInfo.Credits.HasValue)
                            {
                                int sessionsPerClass = CalculateSessionsFromTime(schedule.StartTime, schedule.EndTime);
                                if (sessionsPerClass > 0)
                                {
                                    int totalSessions = classInfo.Credits.Value;
                                    int weeksNeeded = (int)Math.Ceiling((double)totalSessions / sessionsPerClass);
                                    calculatedEndDate = classStartDate.AddDays(weeksNeeded * 7);
                                }
                                else
                                {
                                    calculatedEndDate = weekEndFilter.Value;
                                }
                            }
                            else
                            {
                                calculatedEndDate = weekEndFilter.Value;
                            }
                            
                            // Bỏ qua nếu tuần hiện tại nằm ngoài khoảng thời gian của lớp học
                            if (weekEndFilter.Value < classStartDate || weekStartFilter.Value > calculatedEndDate)
                            {
                                continue;
                            }
                        }

                        var dayName = schedule.DayOfWeek ?? "";
                        if (scheduleByDay.ContainsKey(dayName))
                        {
                            scheduleByDay[dayName].Add(new
                            {
                                ClassCode = classInfo.Code,
                                ClassName = classInfo.Name,
                                CourseName = classInfo.Course.Name,
                                InstructorName = classInfo.Instructor?.FullName ?? "Chưa xác định",
                                StartTime = schedule.StartTime.ToString("HH:mm"),
                                EndTime = schedule.EndTime.ToString("HH:mm"),
                                Room = schedule.Room ?? "Chưa xác định",
                                LessonTitle = schedule.Lesson?.Title ?? "",
                                Status = schedule.Status ?? "Scheduled",
                                ClassId = classInfo.ClassId
                            });
                        }
                    }
                }
                // Nếu không có trong Schedules, lấy từ thông tin Class
                else if (!string.IsNullOrEmpty(classInfo.DayOfWeek) && 
                         classInfo.StartTime.HasValue && 
                         classInfo.EndTime.HasValue)
                {
                    // Kiểm tra xem lớp học có diễn ra trong tuần này không
                    if (weekStartFilter.HasValue && weekEndFilter.HasValue && classInfo.StartDate.HasValue)
                    {
                        var classStartDate = classInfo.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                        
                        // Tính ngày kết thúc thực tế
                        DateTime calculatedEndDate;
                        if (classInfo.EndDate.HasValue)
                        {
                            calculatedEndDate = classInfo.EndDate.Value.ToDateTime(TimeOnly.MinValue);
                        }
                        else if (classInfo.Credits.HasValue)
                        {
                            int sessionsPerClass = CalculateSessionsFromTime(classInfo.StartTime.Value, classInfo.EndTime.Value);
                            if (sessionsPerClass > 0)
                            {
                                int totalSessions = classInfo.Credits.Value;
                                int weeksNeeded = (int)Math.Ceiling((double)totalSessions / sessionsPerClass);
                                calculatedEndDate = classStartDate.AddDays(weeksNeeded * 7);
                            }
                            else
                            {
                                calculatedEndDate = weekEndFilter.Value;
                            }
                        }
                        else
                        {
                            calculatedEndDate = weekEndFilter.Value;
                        }
                        
                        // Bỏ qua nếu tuần hiện tại nằm ngoài khoảng thời gian của lớp học
                        if (weekEndFilter.Value < classStartDate || weekStartFilter.Value > calculatedEndDate)
                        {
                            continue;
                        }
                    }

                    var dayName = classInfo.DayOfWeek;
                    if (scheduleByDay.ContainsKey(dayName))
                    {
                        scheduleByDay[dayName].Add(new
                        {
                            ClassCode = classInfo.Code,
                            ClassName = classInfo.Name,
                            CourseName = classInfo.Course.Name,
                            InstructorName = classInfo.Instructor?.FullName ?? "Chưa xác định",
                            StartTime = classInfo.StartTime.Value.ToString("HH:mm"),
                            EndTime = classInfo.EndTime.Value.ToString("HH:mm"),
                            Room = classInfo.Room ?? "Chưa xác định",
                            LessonTitle = "",
                            Status = "Scheduled",
                            ClassId = classInfo.ClassId
                        });
                    }
                }
            }

            // Sắp xếp các buổi học trong mỗi ngày theo thời gian
            foreach (var day in daysOfWeek)
            {
                scheduleByDay[day] = scheduleByDay[day]
                    .OrderBy(s => s.StartTime)
                    .ToList();
            }

            ViewBag.ScheduleByDay = scheduleByDay;
            ViewBag.DaysOfWeek = daysOfWeek;
            ViewBag.Semesters = semesters;
            ViewBag.CurrentSemester = currentSemester;
            ViewBag.Weeks = weeks;
            ViewBag.CurrentWeek = selectedWeek;
            ViewBag.WeekDates = weekDates;
            ViewBag.IsLoggedIn = isLoggedIn;
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "";
            
            return View();
        }

        /// <summary>
        /// Hiển thị trang điểm danh cho sinh viên
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> StudentAttendance(int classId)
        {
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn") == "true";
            if (!isLoggedIn)
            {
                return RedirectToAction("Login");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Kiểm tra xem sinh viên có trong lớp này không
            var isEnrolled = await _context.ClassStudents
                .AnyAsync(cs => cs.ClassId == classId && cs.StudentId == userId && (cs.Status == "Approved" || cs.Status == "Active"));

            if (!isEnrolled)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập vào lớp học này!";
                return RedirectToAction("MyCourses");
            }

            // Lấy thông tin lớp học
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            // Lấy tất cả các phiên điểm danh của lớp
            var attendances = await _context.Attendances
                .Include(a => a.Lesson)
                .Include(a => a.AttendanceRecords.Where(ar => ar.StudentId == userId))
                .Where(a => a.ClassId == classId)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            // Tính toán thống kê
            var takenSessions = attendances.Count(a => a.AttendanceRecords.Any(ar => ar.StudentId == userId && ar.Status != "Absent"));
            var totalPoints = attendances
                .SelectMany(a => a.AttendanceRecords.Where(ar => ar.StudentId == userId && ar.Status == "Present"))
                .Count();
            var totalSessions = attendances.Count;
            var percentage = totalSessions > 0 ? (double)takenSessions / totalSessions * 100 : 0;

            ViewBag.ClassInfo = classInfo;
            ViewBag.TakenSessions = takenSessions;
            ViewBag.TotalPoints = totalPoints;
            ViewBag.TotalSessions = totalSessions;
            ViewBag.Percentage = percentage;
            ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy", new System.Globalization.CultureInfo("vi-VN"));
            ViewBag.IsLoggedIn = isLoggedIn;
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "";

            return View(attendances);
        }

        /// <summary>
        /// Sinh viên submit điểm danh bằng mã code
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubmitStudentAttendance(int attendanceId, string code)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            // Lấy thông tin phiên điểm danh
            var attendance = await _context.Attendances
                .Include(a => a.AttendanceRecords.Where(ar => ar.StudentId == userId))
                .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId);

            if (attendance == null)
            {
                return Json(new { success = false, message = "Phiên điểm danh không tồn tại!" });
            }

            // Kiểm tra trạng thái
            if (attendance.Status != "Open")
            {
                return Json(new { success = false, message = "Phiên điểm danh đã đóng!" });
            }

            // Kiểm tra thời gian
            if (DateTime.Now < attendance.StartTime)
            {
                return Json(new { success = false, message = "Phiên điểm danh chưa bắt đầu!" });
            }

            if (DateTime.Now > attendance.EndTime)
            {
                return Json(new { success = false, message = "Phiên điểm danh đã kết thúc!" });
            }

            // Kiểm tra mã code
            if (attendance.AllowCodeAttendance && !string.IsNullOrEmpty(attendance.AttendanceCode))
            {
                if (string.IsNullOrWhiteSpace(code) || code.Trim().ToUpper() != attendance.AttendanceCode.ToUpper())
                {
                    return Json(new { success = false, message = "Mã điểm danh không đúng!" });
                }
            }

            // Kiểm tra xem đã điểm danh chưa
            var existingRecord = attendance.AttendanceRecords.FirstOrDefault(ar => ar.StudentId == userId);
            if (existingRecord != null && existingRecord.Status == "Present")
            {
                return Json(new { success = false, message = "Bạn đã điểm danh rồi!" });
            }

            // Cập nhật hoặc tạo mới record
            if (existingRecord != null)
            {
                existingRecord.Status = "Present";
                existingRecord.CheckInTime = DateTime.Now;
                existingRecord.CheckInMethod = "Code";
                existingRecord.UpdatedAt = DateTime.Now;
            }
            else
            {
                var newRecord = new AttendanceRecord
                {
                    AttendanceId = attendanceId,
                    StudentId = userId.Value,
                    Status = "Present",
                    CheckInTime = DateTime.Now,
                    CheckInMethod = "Code",
                    UpdatedAt = DateTime.Now
                };
                _context.AttendanceRecords.Add(newRecord);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Điểm danh thành công!" });
        }

    }

    // Class for attendance code submission
    public class AttendanceCodeSubmission
    {
        public int AttendanceId { get; set; }
        public string Code { get; set; }
    }
}
