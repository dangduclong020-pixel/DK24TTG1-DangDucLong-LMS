using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OfficeOpenXml;
using System.Reflection;

namespace LMS.Controllers
{
    public class TeacherController : Controller
    {
        private readonly LmsSystemContext _context;

        public TeacherController(LmsSystemContext context)
        {
            _context = context;
        }

        // Kiểm tra quyền truy cập giảng viên
        private bool CheckTeacherAccess()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn");
            var userRole = HttpContext.Session.GetString("UserRole");
            var roleId = HttpContext.Session.GetString("RoleId");
            
            return isLoggedIn == "true" && 
                   (roleId == "3" || 
                    userRole == "Giảng viên" || 
                    userRole == "Teacher" || 
                    userRole?.ToLower() == "giang vien");
        }

        private int? GetCurrentTeacherId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (int.TryParse(userIdStr, out var userId))
                return userId;
            return null;
        }

        // Dashboard chính cho giảng viên
        public async Task<IActionResult> Index()
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            if (teacherId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được thông tin giảng viên!";
                return RedirectToAction("Login", "Home");
            }

            // Lấy thông tin giảng viên
            var teacher = await _context.Users
                .Include(u => u.Faculty)
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.UserId == teacherId.Value);

            if (teacher == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin giảng viên!";
                return RedirectToAction("Login", "Home");
            }

            // Thống kê cho dashboard
            var totalClasses = await _context.Classes
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                .CountAsync();

            var totalStudents = await _context.ClassStudents
                .Where(cs => cs.Class.InstructorId == teacherId.Value && cs.Class.DeletedAt == null)
                .CountAsync();

            var totalAssignments = await _context.Assignments
                .Where(a => a.CreatedBy == teacherId.Value && a.DeletedAt == null)
                .CountAsync();

            var pendingSubmissions = await _context.Submissions
                .Where(s => s.Assignment.CreatedBy == teacherId.Value && s.Status == "Pending")
                .CountAsync();

            ViewBag.TeacherName = teacher.FullName;
            ViewBag.FacultyName = teacher.Faculty?.Name ?? "Chưa xác định";
            ViewBag.DepartmentName = teacher.Department?.Name ?? "Chưa xác định";
            ViewBag.TotalClasses = totalClasses;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalAssignments = totalAssignments;
            ViewBag.PendingSubmissions = pendingSubmissions;

            return View();
        }

        // 3.1. Quản lý lớp học - môn học
        public async Task<IActionResult> MyClasses()
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classes = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.ClassStudents)
                .Include(c => c.Assignments)
                .Include(c => c.Exams)
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(classes);
        }

        // Chi tiết lớp học
        public async Task<IActionResult> ClassDetails(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .Include(c => c.Assignments)
                .Include(c => c.Exams)
                .Include(c => c.Lessons)
                .FirstOrDefaultAsync(c => c.ClassId == id && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học hoặc bạn không có quyền truy cập!";
                return RedirectToAction("MyClasses");
            }

            return View(classInfo);
        }

        // Quản lý sinh viên trong lớp
        public async Task<IActionResult> ManageStudents(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.ClassStudents.Where(cs => cs.Status == "Approved"))
                    .ThenInclude(cs => cs.Student)
                        .ThenInclude(s => s.Department)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            // Lấy danh sách yêu cầu tham gia chờ duyệt
            var pendingRequests = await _context.ClassStudents
                .Include(cs => cs.Student)
                    .ThenInclude(s => s.Department)
                .Where(cs => cs.ClassId == classId && cs.Status == "Pending")
                .ToListAsync();

            ViewBag.PendingRequests = pendingRequests;
            ViewBag.ClassInfo = classInfo;

            return View(classInfo);
        }

        // Import sinh viên từ Excel/CSV
        public async Task<IActionResult> ImportStudents(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            ViewBag.ClassInfo = classInfo;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStudents(int classId, IFormFile file)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file để import!";
                return RedirectToAction("ImportStudents", new { classId });
            }

            try
            {
                // Set EPPlus license
                SetEPPlusLicense();

                var successCount = 0;
                var errorList = new List<string>();

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension?.Rows ?? 0;

                        if (rowCount < 2)
                        {
                            TempData["ErrorMessage"] = "File Excel trống hoặc không có dữ liệu!";
                            return RedirectToAction("ImportStudents", new { classId });
                        }

                        // Bỏ qua dòng header, bắt đầu từ dòng 2
                        for (int row = 2; row <= rowCount; row++)
                        {
                            var studentCode = worksheet.Cells[row, 1].Value?.ToString()?.Trim();

                            if (string.IsNullOrWhiteSpace(studentCode))
                            {
                                continue;
                            }

                            // Tìm sinh viên theo mã
                            var student = await _context.Users
                                .FirstOrDefaultAsync(u => u.MssvMgv == studentCode && u.RoleId == 4);

                            if (student == null)
                            {
                                errorList.Add($"Dòng {row}: Không tìm thấy sinh viên {studentCode}");
                                continue;
                            }

                            // Kiểm tra đã tồn tại chưa
                            var existing = await _context.ClassStudents
                                .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == student.UserId);

                            if (existing != null)
                            {
                                errorList.Add($"Dòng {row}: Sinh viên {studentCode} đã có trong lớp");
                                continue;
                            }

                            // Thêm sinh viên vào lớp
                            var classStudent = new ClassStudent
                            {
                                ClassId = classId,
                                StudentId = student.UserId,
                                EnrollDate = DateTime.Now,
                                Status = "Approved"
                            };

                            _context.ClassStudents.Add(classStudent);
                            successCount++;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Cập nhật số lượng sinh viên
                classInfo.CurrentStudents = await _context.ClassStudents
                    .CountAsync(cs => cs.ClassId == classId && cs.Status == "Approved");
                await _context.SaveChangesAsync();

                if (successCount > 0)
                {
                    TempData["SuccessMessage"] = $"Import thành công {successCount} sinh viên!";
                }

                if (errorList.Any())
                {
                    TempData["WarningMessage"] = $"Có {errorList.Count} lỗi: " + string.Join(", ", errorList.Take(3));
                }

                return RedirectToAction("ManageStudents", new { classId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi import: {ex.Message}";
                return RedirectToAction("ImportStudents", new { classId });
            }
        }

        // Xuất danh sách sinh viên ra Excel
        public async Task<IActionResult> ExportStudentsToExcel(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var classInfo = await _context.Classes
                    .Include(c => c.Course)
                    .Include(c => c.ClassStudents.Where(cs => cs.Status == "Active" || cs.Status == "Approved"))
                        .ThenInclude(cs => cs.Student)
                            .ThenInclude(s => s.Department)
                    .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

                if (classInfo == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                    return RedirectToAction("MyClasses");
                }

                SetEPPlusLicense();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Danh sách sinh viên");

                    // Header
                    worksheet.Cells[1, 1].Value = "STT";
                    worksheet.Cells[1, 2].Value = "MSSV";
                    worksheet.Cells[1, 3].Value = "Họ và tên";
                    worksheet.Cells[1, 4].Value = "Email";
                    worksheet.Cells[1, 5].Value = "Số điện thoại";
                    worksheet.Cells[1, 6].Value = "Chuyên ngành";
                    worksheet.Cells[1, 7].Value = "Ngày tham gia";

                    // Style header
                    using (var range = worksheet.Cells[1, 1, 1, 7])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                        range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }

                    // Data
                    int row = 2;
                    int stt = 1;
                    foreach (var cs in classInfo.ClassStudents.OrderBy(c => c.Student.MssvMgv))
                    {
                        worksheet.Cells[row, 1].Value = stt;
                        worksheet.Cells[row, 2].Value = cs.Student?.MssvMgv ?? "";
                        worksheet.Cells[row, 3].Value = cs.Student?.FullName ?? "";
                        worksheet.Cells[row, 4].Value = cs.Student?.Email ?? "";
                        worksheet.Cells[row, 5].Value = cs.Student?.Phone ?? "";
                        worksheet.Cells[row, 6].Value = cs.Student?.Department?.Name ?? "";
                        worksheet.Cells[row, 7].Value = cs.EnrollDate?.ToString("dd/MM/yyyy") ?? "";
                        
                        row++;
                        stt++;
                    }

                    // Auto-fit columns
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    // Thêm border cho toàn bộ bảng
                    using (var range = worksheet.Cells[1, 1, row - 1, 7])
                    {
                        range.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }

                    // Generate file name
                    var fileName = $"DanhSachSinhVien_{classInfo.Code}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    var fileBytes = package.GetAsByteArray();

                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xuất file Excel: {ex.Message}";
                return RedirectToAction("ClassDetails", new { id = classId });
            }
        }

        // Xác nhận yêu cầu tham gia
        [HttpPost]
        public async Task<IActionResult> ApproveJoinRequest(int classId, int studentId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var classInfo = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

                if (classInfo == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học" });
                }

                var request = await _context.ClassStudents
                    .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);

                if (request == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu" });
                }

                request.Status = "Approved";
                request.EnrollDate = DateTime.Now;

                // Cập nhật số lượng sinh viên
                classInfo.CurrentStudents = await _context.ClassStudents
                    .CountAsync(cs => cs.ClassId == classId && cs.Status == "Approved");

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã phê duyệt yêu cầu" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Từ chối yêu cầu tham gia
        [HttpPost]
        public async Task<IActionResult> RejectJoinRequest(int classId, int studentId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var classInfo = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

                if (classInfo == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học" });
                }

                var request = await _context.ClassStudents
                    .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);

                if (request != null)
                {
                    _context.ClassStudents.Remove(request);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Đã từ chối yêu cầu" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Xóa sinh viên khỏi lớp
        [HttpPost]
        public async Task<IActionResult> RemoveStudent(int classId, int studentId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var classInfo = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

                if (classInfo == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học" });
                }

                var classStudent = await _context.ClassStudents
                    .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);

                if (classStudent != null)
                {
                    _context.ClassStudents.Remove(classStudent);
                    
                    // Cập nhật số lượng
                    classInfo.CurrentStudents = await _context.ClassStudents
                        .CountAsync(cs => cs.ClassId == classId && cs.Status == "Approved");
                    
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Đã xóa sinh viên khỏi lớp" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 3.2. Quản lý bài tập
        public async Task<IActionResult> Assignments()
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var assignments = await _context.Assignments
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Where(a => a.CreatedBy == teacherId.Value && a.DeletedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(assignments);
        }

        // Tạo bài tập mới
        public async Task<IActionResult> CreateAssignment()
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classes = await _context.Classes
                .Include(c => c.Course)
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                .ToListAsync();

            ViewBag.Classes = new SelectList(classes, "ClassId", "Course.Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateAssignment(Assignment assignment, IFormFile? attachmentFile)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                assignment.CreatedBy = teacherId.Value;
                assignment.CreatedAt = DateTime.Now;
                assignment.IsActive = true;

                // Xử lý file đính kèm
                if (attachmentFile != null && attachmentFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "assignments");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + attachmentFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await attachmentFile.CopyToAsync(fileStream);
                    }

                    assignment.AttachmentPath = "/uploads/assignments/" + uniqueFileName;
                    assignment.AttachmentName = attachmentFile.FileName;
                }

                _context.Assignments.Add(assignment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo bài tập thành công!";
                return RedirectToAction("Assignments");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo bài tập: {ex.Message}";
                
                // Reload data for dropdown
                var teacherId = GetCurrentTeacherId();
                var classes = await _context.Classes
                    .Include(c => c.Course)
                    .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                    .ToListAsync();
                ViewBag.Classes = new SelectList(classes, "ClassId", "Course.Name");
                
                return View(assignment);
            }
        }

        // Xem bài nộp của sinh viên
        public async Task<IActionResult> AssignmentSubmissions(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var assignment = await _context.Assignments
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(a => a.AssignmentId == id && a.CreatedBy == teacherId.Value);

            if (assignment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài tập!";
                return RedirectToAction("Assignments");
            }

            return View(assignment);
        }

        // Chấm điểm bài tập
        [HttpPost]
        public async Task<IActionResult> GradeSubmission(int submissionId, decimal score, string? feedback)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var submission = await _context.Submissions
                    .Include(s => s.Assignment)
                    .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && s.Assignment.CreatedBy == teacherId.Value);

                if (submission == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bài nộp!" });
                }

                submission.Score = score;
                submission.Feedback = feedback;
                submission.Status = "Graded";
                submission.GradedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Chấm điểm thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // 3.3. Quản lý đề thi và ngân hàng câu hỏi
        public async Task<IActionResult> Exams()
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var exams = await _context.Exams
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Where(e => e.CreatedBy == teacherId.Value && e.DeletedAt == null)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return View(exams);
        }

        // Ngân hàng câu hỏi
        public async Task<IActionResult> QuestionBank(int? courseId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            
            // Lấy danh sách các môn học giảng viên đang dạy
            var myCourses = await _context.Classes
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                .Select(c => c.Course)
                .Distinct()
                .ToListAsync();

            ViewBag.MyCourses = new SelectList(myCourses, "CourseId", "Name", courseId);

            // Lọc theo môn học nếu có
            var query = _context.QuestionBanks
                .Include(q => q.Course)
                .Include(q => q.Difficulty)
                .Where(q => q.CreatedBy == teacherId.Value && q.DeletedAt == null);

            if (courseId.HasValue)
            {
                query = query.Where(q => q.CourseId == courseId.Value);
            }

            var questions = await query
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            ViewBag.SelectedCourseId = courseId;
            return View(questions);
        }

        // Tạo câu hỏi mới cho ngân hàng
        public async Task<IActionResult> CreateQuestion(int? courseId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            
            // Lấy danh sách môn học giảng viên đang dạy
            var myCourses = await _context.Classes
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                .Select(c => c.Course)
                .Distinct()
                .ToListAsync();

            ViewBag.Courses = new SelectList(myCourses, "CourseId", "Name", courseId);
            
            // Lấy danh sách độ khó
            var difficulties = await _context.DifficultyLevels.ToListAsync();
            ViewBag.Difficulties = new SelectList(difficulties, "DifficultyId", "Name");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestion(QuestionBank question)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                question.CreatedBy = teacherId.Value;
                question.CreatedAt = DateTime.Now;

                _context.QuestionBanks.Add(question);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo câu hỏi thành công!";
                return RedirectToAction("QuestionBank", new { courseId = question.CourseId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo câu hỏi: {ex.Message}";
                
                var teacherId = GetCurrentTeacherId();
                var myCourses = await _context.Classes
                    .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                    .Select(c => c.Course)
                    .Distinct()
                    .ToListAsync();

                ViewBag.Courses = new SelectList(myCourses, "CourseId", "Name");
                var difficulties = await _context.DifficultyLevels.ToListAsync();
                ViewBag.Difficulties = new SelectList(difficulties, "DifficultyId", "Name");
                
                return View(question);
            }
        }

        // Sửa câu hỏi
        public async Task<IActionResult> EditQuestion(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var question = await _context.QuestionBanks
                .FirstOrDefaultAsync(q => q.QuestionId == id && q.CreatedBy == teacherId.Value);

            if (question == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy câu hỏi!";
                return RedirectToAction("QuestionBank");
            }

            var myCourses = await _context.Classes
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                .Select(c => c.Course)
                .Distinct()
                .ToListAsync();

            ViewBag.Courses = new SelectList(myCourses, "CourseId", "Name", question.CourseId);
            
            var difficulties = await _context.DifficultyLevels.ToListAsync();
            ViewBag.Difficulties = new SelectList(difficulties, "DifficultyId", "Name", question.DifficultyId);

            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestion(QuestionBank question)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var existingQuestion = await _context.QuestionBanks
                    .FirstOrDefaultAsync(q => q.QuestionId == question.QuestionId && q.CreatedBy == teacherId.Value);

                if (existingQuestion == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy câu hỏi!";
                    return RedirectToAction("QuestionBank");
                }

                existingQuestion.Content = question.Content;
                existingQuestion.Type = question.Type;
                existingQuestion.Answer = question.Answer;
                existingQuestion.Options = question.Options;
                existingQuestion.Explanation = question.Explanation;
                existingQuestion.MaxScore = question.MaxScore;
                existingQuestion.DifficultyId = question.DifficultyId;
                existingQuestion.CourseId = question.CourseId;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật câu hỏi thành công!";
                return RedirectToAction("QuestionBank", new { courseId = question.CourseId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật câu hỏi: {ex.Message}";
                return RedirectToAction("EditQuestion", new { id = question.QuestionId });
            }
        }

        // Xóa câu hỏi
        [HttpPost]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var question = await _context.QuestionBanks
                    .FirstOrDefaultAsync(q => q.QuestionId == id && q.CreatedBy == teacherId.Value);

                if (question == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy câu hỏi" });
                }

                question.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa câu hỏi thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== 3.1. QUẢN LÝ NỘI DUNG GIẢNG DẠY (LESSONS) =====
        
        // Danh sách bài học của lớp
        public async Task<IActionResult> ClassLessons(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            var lessons = await _context.Lessons
                .Include(l => l.LessonFiles)
                .Where(l => l.ClassId == classId && l.DeletedAt == null)
                .OrderBy(l => l.Week)
                .ThenBy(l => l.OrderNumber)
                .ToListAsync();

            ViewBag.ClassInfo = classInfo;
            return View(lessons);
        }

        // Tạo chủ đề/chương học mới
        public async Task<IActionResult> CreateLesson(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            ViewBag.ClassInfo = classInfo;
            
            // Lấy số tuần tiếp theo
            var lastLesson = await _context.Lessons
                .Where(l => l.ClassId == classId && l.DeletedAt == null)
                .OrderByDescending(l => l.Week)
                .ThenByDescending(l => l.OrderNumber)
                .FirstOrDefaultAsync();

            ViewBag.SuggestedWeek = lastLesson != null ? lastLesson.Week + 1 : 1;
            ViewBag.SuggestedOrder = 1;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLesson(Lesson lesson, List<IFormFile>? lessonFiles)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var classInfo = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == lesson.ClassId && c.InstructorId == teacherId.Value);

                if (classInfo == null)
                {
                    TempData["ErrorMessage"] = "Không có quyền tạo bài học cho lớp này!";
                    return RedirectToAction("MyClasses");
                }

                lesson.CreatedAt = DateTime.Now;
                lesson.IsPublished = true;

                _context.Lessons.Add(lesson);
                await _context.SaveChangesAsync();

                // Xử lý upload files (video, PDF, slide)
                if (lessonFiles != null && lessonFiles.Any())
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "lessons");
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var file in lessonFiles)
                    {
                        if (file.Length > 0)
                        {
                            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            var lessonFile = new LessonFile
                            {
                                LessonId = lesson.LessonId,
                                FileName = file.FileName,
                                FilePath = "/uploads/lessons/" + uniqueFileName,
                                FileSize = (int)file.Length,
                                FileType = Path.GetExtension(file.FileName),
                                Mimetype = file.ContentType,
                                UploadedAt = DateTime.Now
                            };

                            _context.LessonFiles.Add(lessonFile);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Tạo bài học thành công!";
                return RedirectToAction("ClassLessons", new { classId = lesson.ClassId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo bài học: {ex.Message}";
                return RedirectToAction("CreateLesson", new { classId = lesson.ClassId });
            }
        }

        // Sửa bài học
        public async Task<IActionResult> EditLesson(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var lesson = await _context.Lessons
                .Include(l => l.Class)
                    .ThenInclude(c => c.Course)
                .Include(l => l.LessonFiles)
                .FirstOrDefaultAsync(l => l.LessonId == id && l.Class.InstructorId == teacherId.Value);

            if (lesson == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài học!";
                return RedirectToAction("MyClasses");
            }

            ViewBag.ClassInfo = lesson.Class;
            return View(lesson);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLesson(Lesson lesson, List<IFormFile>? newFiles)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var existingLesson = await _context.Lessons
                    .Include(l => l.Class)
                    .FirstOrDefaultAsync(l => l.LessonId == lesson.LessonId && l.Class.InstructorId == teacherId.Value);

                if (existingLesson == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy bài học!";
                    return RedirectToAction("MyClasses");
                }

                existingLesson.Title = lesson.Title;
                existingLesson.Content = lesson.Content;
                existingLesson.Week = lesson.Week;
                existingLesson.OrderNumber = lesson.OrderNumber;
                existingLesson.Duration = lesson.Duration;
                existingLesson.IsPublished = lesson.IsPublished;
                existingLesson.UpdatedAt = DateTime.Now;

                // Upload thêm file mới nếu có
                if (newFiles != null && newFiles.Any())
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "lessons");
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var file in newFiles)
                    {
                        if (file.Length > 0)
                        {
                            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            var lessonFile = new LessonFile
                            {
                                LessonId = lesson.LessonId,
                                FileName = file.FileName,
                                FilePath = "/uploads/lessons/" + uniqueFileName,
                                FileSize = (int)file.Length,
                                FileType = Path.GetExtension(file.FileName),
                                Mimetype = file.ContentType,
                                UploadedAt = DateTime.Now
                            };

                            _context.LessonFiles.Add(lessonFile);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật bài học thành công!";
                return RedirectToAction("ClassLessons", new { classId = existingLesson.ClassId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật bài học: {ex.Message}";
                return RedirectToAction("EditLesson", new { id = lesson.LessonId });
            }
        }

        // Xóa file của bài học
        [HttpPost]
        public async Task<IActionResult> DeleteLessonFile(int fileId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var lessonFile = await _context.LessonFiles
                    .Include(lf => lf.Lesson)
                        .ThenInclude(l => l.Class)
                    .FirstOrDefaultAsync(lf => lf.FileId == fileId && lf.Lesson.Class.InstructorId == teacherId.Value);

                if (lessonFile == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy file" });
                }

                // Xóa file vật lý
                var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", lessonFile.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }

                _context.LessonFiles.Remove(lessonFile);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa file thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Xóa bài học
        [HttpPost]
        public async Task<IActionResult> DeleteLesson(int id)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var lesson = await _context.Lessons
                    .Include(l => l.Class)
                    .FirstOrDefaultAsync(l => l.LessonId == id && l.Class.InstructorId == teacherId.Value);

                if (lesson == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bài học" });
                }

                lesson.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa bài học thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== 3.2. QUẢN LÝ BÀI TẬP (đã có sẵn, bổ sung thêm) =====
        
        // Danh sách bài tập của một lớp cụ thể
        public async Task<IActionResult> ClassAssignments(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            var assignments = await _context.Assignments
                .Include(a => a.Submissions)
                .Where(a => a.ClassId == classId && a.DeletedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewBag.ClassInfo = classInfo;
            return View(assignments);
        }

        // Sửa bài tập
        public async Task<IActionResult> EditAssignment(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var assignment = await _context.Assignments
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .FirstOrDefaultAsync(a => a.AssignmentId == id && a.CreatedBy == teacherId.Value);

            if (assignment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài tập!";
                return RedirectToAction("Assignments");
            }

            ViewBag.ClassInfo = assignment.Class;
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAssignment(Assignment assignment, IFormFile? newAttachment)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var existingAssignment = await _context.Assignments
                    .FirstOrDefaultAsync(a => a.AssignmentId == assignment.AssignmentId && a.CreatedBy == teacherId.Value);

                if (existingAssignment == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy bài tập!";
                    return RedirectToAction("Assignments");
                }

                existingAssignment.Title = assignment.Title;
                existingAssignment.Description = assignment.Description;
                existingAssignment.DueDate = assignment.DueDate;
                existingAssignment.MaxScore = assignment.MaxScore;
                existingAssignment.MaxAttempts = assignment.MaxAttempts;
                existingAssignment.AllowLateSubmission = assignment.AllowLateSubmission;
                existingAssignment.LatePenalty = assignment.LatePenalty;
                existingAssignment.Weightage = assignment.Weightage;
                existingAssignment.IsPublished = assignment.IsPublished;

                // Upload file đính kèm mới nếu có
                if (newAttachment != null && newAttachment.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "assignments");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + newAttachment.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await newAttachment.CopyToAsync(fileStream);
                    }

                    // Xóa file cũ nếu có
                    if (!string.IsNullOrEmpty(existingAssignment.AttachmentPath))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingAssignment.AttachmentPath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    existingAssignment.AttachmentPath = "/uploads/assignments/" + uniqueFileName;
                    existingAssignment.AttachmentName = newAttachment.FileName;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật bài tập thành công!";
                return RedirectToAction("ClassAssignments", new { classId = existingAssignment.ClassId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật bài tập: {ex.Message}";
                return RedirectToAction("EditAssignment", new { id = assignment.AssignmentId });
            }
        }

        // Xóa bài tập
        [HttpPost]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var assignment = await _context.Assignments
                    .FirstOrDefaultAsync(a => a.AssignmentId == id && a.CreatedBy == teacherId.Value);

                if (assignment == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bài tập" });
                }

                assignment.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa bài tập thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Xuất điểm bài tập ra Excel
        public async Task<IActionResult> ExportAssignmentGrades(int assignmentId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var assignment = await _context.Assignments
                    .Include(a => a.Class)
                        .ThenInclude(c => c.Course)
                    .Include(a => a.Submissions)
                        .ThenInclude(s => s.Student)
                    .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.CreatedBy == teacherId.Value);

                if (assignment == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy bài tập!";
                    return RedirectToAction("Assignments");
                }

                SetEPPlusLicense();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Điểm bài tập");

                    // Header
                    worksheet.Cells[1, 1].Value = "Môn học: " + assignment.Class.Course.Name;
                    worksheet.Cells[2, 1].Value = "Lớp: " + assignment.Class.Name;
                    worksheet.Cells[3, 1].Value = "Bài tập: " + assignment.Title;
                    worksheet.Cells[4, 1].Value = "Ngày xuất: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                    // Column headers
                    worksheet.Cells[6, 1].Value = "STT";
                    worksheet.Cells[6, 2].Value = "MSSV";
                    worksheet.Cells[6, 3].Value = "Họ và tên";
                    worksheet.Cells[6, 4].Value = "Email";
                    worksheet.Cells[6, 5].Value = "Điểm";
                    worksheet.Cells[6, 6].Value = "Ngày nộp";
                    worksheet.Cells[6, 7].Value = "Trạng thái";
                    worksheet.Cells[6, 8].Value = "Nhận xét";

                    // Data
                    int row = 7;
                    int stt = 1;
                    foreach (var submission in assignment.Submissions.OrderBy(s => s.Student.FullName))
                    {
                        worksheet.Cells[row, 1].Value = stt++;
                        worksheet.Cells[row, 2].Value = submission.Student.MssvMgv;
                        worksheet.Cells[row, 3].Value = submission.Student.FullName;
                        worksheet.Cells[row, 4].Value = submission.Student.Email;
                        worksheet.Cells[row, 5].Value = submission.Score?.ToString() ?? "";
                        worksheet.Cells[row, 6].Value = submission.SubmittedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
                        worksheet.Cells[row, 7].Value = submission.Status;
                        worksheet.Cells[row, 8].Value = submission.Feedback ?? "";
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Cells.AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    var fileName = $"Diem_BaiTap_{assignment.Title}_{DateTime.Now:yyyyMMdd}.xlsx";
                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xuất file: {ex.Message}";
                return RedirectToAction("AssignmentSubmissions", new { id = assignmentId });
            }
        }

        // ===== 3.3. TẠO ĐỀ THI - THI ONLINE =====

        // Danh sách đề thi của một lớp cụ thể
        public async Task<IActionResult> ClassExams(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            var exams = await _context.Exams
                .Include(e => e.ExamQuestions)
                .Include(e => e.ExamResults)
                .Where(e => e.ClassId == classId && e.DeletedAt == null)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            ViewBag.ClassInfo = classInfo;
            return View(exams);
        }

        // Tạo đề thi mới
        public async Task<IActionResult> CreateExam(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            // Lấy danh sách câu hỏi từ ngân hàng theo môn học
            var questions = await _context.QuestionBanks
                .Include(q => q.Difficulty)
                .Where(q => q.CourseId == classInfo.CourseId && q.DeletedAt == null)
                .OrderBy(q => q.DifficultyId)
                .ThenBy(q => q.CreatedAt)
                .ToListAsync();

            ViewBag.ClassInfo = classInfo;
            ViewBag.QuestionBank = questions;
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExam(Exam exam, List<int>? selectedQuestions)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var classInfo = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == exam.ClassId && c.InstructorId == teacherId.Value);

                if (classInfo == null)
                {
                    TempData["ErrorMessage"] = "Không có quyền tạo đề thi cho lớp này!";
                    return RedirectToAction("MyClasses");
                }

                exam.CreatedBy = teacherId.Value;
                exam.CreatedAt = DateTime.Now;
                exam.IsPublished = true;

                _context.Exams.Add(exam);
                await _context.SaveChangesAsync();

                // Thêm câu hỏi vào đề thi (nếu chọn từ ngân hàng)
                if (selectedQuestions != null && selectedQuestions.Any())
                {
                    int order = 1;
                    decimal scorePerQuestion = exam.MaxScore / selectedQuestions.Count;

                    // Trộn câu hỏi ngẫu nhiên
                    var random = new Random();
                    var shuffledQuestions = selectedQuestions.OrderBy(x => random.Next()).ToList();

                    foreach (var questionId in shuffledQuestions)
                    {
                        var examQuestion = new ExamQuestion
                        {
                            ExamId = exam.ExamId,
                            QuestionId = questionId,
                            QuestionOrder = order++,
                            QuestionScore = scorePerQuestion
                        };
                        _context.ExamQuestions.Add(examQuestion);
                    }

                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Tạo đề thi thành công!";
                return RedirectToAction("ClassExams", new { classId = exam.ClassId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo đề thi: {ex.Message}";
                return RedirectToAction("CreateExam", new { classId = exam.ClassId });
            }
        }

        // Sửa đề thi
        public async Task<IActionResult> EditExam(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var exam = await _context.Exams
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.CreatedBy == teacherId.Value);

            if (exam == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đề thi!";
                return RedirectToAction("Exams");
            }

            // Lấy danh sách câu hỏi từ ngân hàng
            var questions = await _context.QuestionBanks
                .Include(q => q.Difficulty)
                .Where(q => q.CourseId == exam.Class.CourseId && q.DeletedAt == null)
                .OrderBy(q => q.DifficultyId)
                .ToListAsync();

            ViewBag.ClassInfo = exam.Class;
            ViewBag.QuestionBank = questions;
            ViewBag.SelectedQuestions = exam.ExamQuestions.Select(eq => eq.QuestionId).ToList();

            return View(exam);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditExam(Exam exam, List<int>? selectedQuestions)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var existingExam = await _context.Exams
                    .Include(e => e.ExamQuestions)
                    .Include(e => e.Class)
                    .FirstOrDefaultAsync(e => e.ExamId == exam.ExamId && e.CreatedBy == teacherId.Value);

                if (existingExam == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy đề thi!";
                    return RedirectToAction("Exams");
                }

                existingExam.Title = exam.Title;
                existingExam.Description = exam.Description;
                existingExam.StartTime = exam.StartTime;
                existingExam.EndTime = exam.EndTime;
                existingExam.Duration = exam.Duration;
                existingExam.TotalQuestions = exam.TotalQuestions;
                existingExam.MaxScore = exam.MaxScore;
                existingExam.Conditions = exam.Conditions;
                existingExam.AllowMultipleAttempts = exam.AllowMultipleAttempts;
                existingExam.IsPublished = exam.IsPublished;

                // Cập nhật danh sách câu hỏi
                if (selectedQuestions != null)
                {
                    // Xóa câu hỏi cũ
                    _context.ExamQuestions.RemoveRange(existingExam.ExamQuestions);

                    // Thêm câu hỏi mới (trộn ngẫu nhiên)
                    var random = new Random();
                    var shuffledQuestions = selectedQuestions.OrderBy(x => random.Next()).ToList();
                    
                    int order = 1;
                    decimal scorePerQuestion = exam.MaxScore / selectedQuestions.Count;

                    foreach (var questionId in shuffledQuestions)
                    {
                        var examQuestion = new ExamQuestion
                        {
                            ExamId = exam.ExamId,
                            QuestionId = questionId,
                            QuestionOrder = order++,
                            QuestionScore = scorePerQuestion
                        };
                        _context.ExamQuestions.Add(examQuestion);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật đề thi thành công!";
                return RedirectToAction("ClassExams", new { classId = existingExam.ClassId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật đề thi: {ex.Message}";
                return RedirectToAction("EditExam", new { id = exam.ExamId });
            }
        }

        // Xóa đề thi
        [HttpPost]
        public async Task<IActionResult> DeleteExam(int id)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var exam = await _context.Exams
                    .FirstOrDefaultAsync(e => e.ExamId == id && e.CreatedBy == teacherId.Value);

                if (exam == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đề thi" });
                }

                exam.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa đề thi thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Xem kết quả thi của sinh viên
        public async Task<IActionResult> ExamResults(int examId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var exam = await _context.Exams
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.ExamResults)
                    .ThenInclude(er => er.Student)
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.CreatedBy == teacherId.Value);

            if (exam == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đề thi!";
                return RedirectToAction("Exams");
            }

            return View(exam);
        }

        // Chấm tự luận thủ công
        public async Task<IActionResult> GradeEssayExam(int resultId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var result = await _context.ExamResults
                .Include(er => er.Student)
                .Include(er => er.Exam)
                    .ThenInclude(e => e.Class)
                .Include(er => er.ExamAnswers)
                    .ThenInclude(ea => ea.Question)
                .FirstOrDefaultAsync(er => er.ResultId == resultId && er.Exam.CreatedBy == teacherId.Value);

            if (result == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài thi!";
                return RedirectToAction("Exams");
            }

            return View(result);
        }

        [HttpPost]
        public async Task<IActionResult> GradeEssayAnswer(int answerId, decimal score, string? feedback)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var answer = await _context.ExamAnswers
                    .Include(ea => ea.Result)
                        .ThenInclude(r => r.Exam)
                    .FirstOrDefaultAsync(ea => ea.AnswerId == answerId && ea.Result.Exam.CreatedBy == teacherId.Value);

                if (answer == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy câu trả lời" });
                }

                answer.QuestionScore = score;
                // Note: ExamAnswer không có trường TeacherFeedback, cần thêm vào DB nếu cần

                // Cập nhật tổng điểm của bài thi
                var result = answer.Result;
                var totalScore = await _context.ExamAnswers
                    .Where(ea => ea.ResultId == result.ResultId && ea.QuestionScore.HasValue)
                    .SumAsync(ea => ea.QuestionScore.Value);

                result.Score = totalScore;
                result.Status = "Graded";

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Chấm điểm thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== 3.4. THEO DÕI HỌC VIÊN =====
        
        public async Task<IActionResult> StudentProgress(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            // Lấy thông tin tiến độ học tập
            var studentProgress = new List<dynamic>();
            foreach (var cs in classInfo.ClassStudents)
            {
                var totalAssignments = await _context.Assignments
                    .Where(a => a.ClassId == classId && a.DeletedAt == null)
                    .CountAsync();

                var completedAssignments = await _context.Submissions
                    .Where(s => s.StudentId == cs.StudentId && s.Assignment.ClassId == classId)
                    .CountAsync();

                var averageScore = await _context.Submissions
                    .Where(s => s.StudentId == cs.StudentId && s.Assignment.ClassId == classId && s.Score.HasValue)
                    .AverageAsync(s => s.Score) ?? 0;

                studentProgress.Add(new
                {
                    Student = cs.Student,
                    TotalAssignments = totalAssignments,
                    CompletedAssignments = completedAssignments,
                    AverageScore = averageScore,
                    ProgressPercentage = totalAssignments > 0 ? (completedAssignments * 100.0 / totalAssignments) : 0
                });
            }

            ViewBag.ClassInfo = classInfo;
            ViewBag.StudentProgress = studentProgress;

            return View();
        }

        // Gửi thông báo cho sinh viên
        public async Task<IActionResult> SendNotification(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            ViewBag.ClassInfo = classInfo;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendNotification(int classId, string title, string content)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var students = await _context.ClassStudents
                    .Where(cs => cs.ClassId == classId)
                    .Select(cs => cs.StudentId)
                    .ToListAsync();

                // Tạo thông báo cho từng sinh viên
                foreach (var studentId in students)
                {
                    var notification = new Notification
                    {
                        Title = title,
                        Content = content,
                        SenderId = teacherId.Value,
                        ReceiverId = studentId,
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notification);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã gửi thông báo cho {students.Count} sinh viên!";
                return RedirectToAction("MyClasses");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi gửi thông báo: {ex.Message}";
                return RedirectToAction("SendNotification", new { classId });
            }
        }

        // Helper method to set EPPlus license
        private void SetEPPlusLicense()
        {
            try
            {
                var licenseContextProperty = typeof(ExcelPackage).GetProperty("LicenseContext",
                    BindingFlags.Static | BindingFlags.Public);

                if (licenseContextProperty != null)
                {
                    var licenseContextType = licenseContextProperty.PropertyType;
                    var nonCommercialValue = Enum.Parse(licenseContextType, "NonCommercial");
                    licenseContextProperty.SetValue(null, nonCommercialValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EPPlus License Error: {ex.Message}");
            }
        }

        // Đăng xuất
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}