using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OfficeOpenXml;
using System.Reflection;
using QRCoder;

namespace LMS.Controllers
{
    public class TeacherController : BaseController
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
            return HttpContext.Session.GetInt32("UserId");
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
                .Include(c => c.Assignments.Where(a => a.DeletedAt == null))
                .Include(c => c.Exams.Where(e => e.DeletedAt == null))
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
                .AsSplitQuery()  // Tách thành nhiều query để tránh timeout
                .Include(c => c.Course)
                    .ThenInclude(co => co.Faculty)
                .Include(c => c.Instructor)
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .Include(c => c.Assignments)
                    .ThenInclude(a => a.Submissions)
                .Include(c => c.Exams.Where(e => e.DeletedAt == null))
                    .ThenInclude(e => e.ExamResults)
                .Include(c => c.Exams.Where(e => e.DeletedAt == null))
                    .ThenInclude(e => e.ExamQuestions)
                .Include(c => c.Lessons.Where(l => l.DeletedAt == null))
                    .ThenInclude(l => l.LessonFiles)
                .Include(c => c.Attendances)
                    .ThenInclude(a => a.AttendanceRecords)
                .Include(c => c.Attendances)
                    .ThenInclude(a => a.Lesson)
                .FirstOrDefaultAsync(c => c.ClassId == id && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học hoặc bạn không có quyền truy cập!";
                return RedirectToAction("MyClasses");
            }

            // Sử dụng chung view CourseDetail với sinh viên, giảng viên sẽ thấy thêm nút xóa
            return View("~/Views/Home/CourseDetail.cshtml", classInfo);
        }

        // Chỉnh sửa thông tin lớp học (Description và Objectives)
        public async Task<IActionResult> EditClass(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                    .ThenInclude(co => co.Faculty)
                .Include(c => c.Instructor)
                .Include(c => c.Lessons.Where(l => l.DeletedAt == null))
                    .ThenInclude(l => l.LessonFiles)
                .Include(c => c.Assignments.Where(a => a.DeletedAt == null))
                    .ThenInclude(a => a.Submissions)
                .Include(c => c.Exams.Where(e => e.DeletedAt == null))
                    .ThenInclude(e => e.ExamResults)
                .Include(c => c.Attendances)
                    .ThenInclude(a => a.AttendanceRecords)
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefaultAsync(c => c.ClassId == id && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học hoặc bạn không có quyền truy cập!";
                return RedirectToAction("MyClasses");
            }

            return View(classInfo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClass(int ClassId, string? Description, string? Objectives, 
            string? IntroVideoUrl, string? SlidesUrl, string? LectureVideoUrl,
            List<LessonDto>? ExistingLessons, List<LessonDto>? NewChapters)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                
                var existingClass = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == ClassId && c.InstructorId == teacherId.Value);

                if (existingClass == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy lớp học hoặc bạn không có quyền truy cập!";
                    return RedirectToAction("MyClasses");
                }

                // Debug log
                System.Diagnostics.Debug.WriteLine($"ExistingLessons count: {ExistingLessons?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"NewChapters count: {NewChapters?.Count ?? 0}");
                existingClass.Description = Description;
                existingClass.Objectives = Objectives;
                existingClass.IntroVideoUrl = IntroVideoUrl;
                existingClass.SlidesUrl = SlidesUrl;
                existingClass.LectureVideoUrl = LectureVideoUrl;

                await _context.SaveChangesAsync();

                // Cập nhật các chương hiện có
                if (ExistingLessons != null && ExistingLessons.Any())
                {
                    foreach (var lessonDto in ExistingLessons)
                    {
                        if (lessonDto.LessonId.HasValue)
                        {
                            // Nếu LessonId âm, đánh dấu xóa (soft delete)
                            if (lessonDto.LessonId.Value < 0)
                            {
                                var lessonIdToDelete = Math.Abs(lessonDto.LessonId.Value);
                                var lessonToDelete = await _context.Lessons.FindAsync(lessonIdToDelete);
                                if (lessonToDelete != null && lessonToDelete.ClassId == ClassId)
                                {
                                    lessonToDelete.DeletedAt = DateTime.Now;
                                }
                            }
                            else
                            {
                                // Cập nhật chương hiện có
                                var lesson = await _context.Lessons.FindAsync(lessonDto.LessonId.Value);
                                if (lesson != null && lesson.ClassId == ClassId)
                                {
                                    lesson.Title = lessonDto.Title;
                                    lesson.Content = lessonDto.Content;
                                    lesson.VideoUrl = lessonDto.VideoUrl;
                                    lesson.OrderNumber = lessonDto.OrderNumber; // Cập nhật OrderNumber
                                    lesson.UpdatedAt = DateTime.Now;
                                }
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // Thêm các chương mới nếu có
                if (NewChapters != null && NewChapters.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Adding {NewChapters.Count} new chapters");
                    foreach (var chapter in NewChapters)
                    {
                        System.Diagnostics.Debug.WriteLine($"New Chapter - Title: {chapter.Title}, Content length: {chapter.Content?.Length ?? 0}");
                        
                        var newLesson = new Lesson
                        {
                            ClassId = ClassId,
                            Title = chapter.Title ?? $"Chương {chapter.OrderNumber}",
                            Content = chapter.Content,
                            VideoUrl = chapter.VideoUrl,
                            OrderNumber = chapter.OrderNumber,
                            Week = chapter.Week ?? 1,
                            Duration = chapter.Duration,
                            IsPublished = true,
                            CreatedAt = DateTime.Now
                        };

                        _context.Lessons.Add(newLesson);
                    }

                    await _context.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine("New chapters saved successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No new chapters to add");
                }

                TempData["SuccessMessage"] = "Cập nhật thông tin lớp học thành công!";
                
                return RedirectToAction("ClassDetails", new { id = ClassId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật lớp học: {ex.Message}";
                return RedirectToAction("EditClass", new { id = ClassId });
            }
        }

        // POST: Cập nhật video URL cho lesson
        [HttpPost]
        public async Task<IActionResult> UpdateLessonVideo([FromBody] UpdateLessonVideoRequest request)
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
                    .FirstOrDefaultAsync(l => l.LessonId == request.LessonId && l.Class.InstructorId == teacherId.Value);

                if (lesson == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy chương học hoặc bạn không có quyền truy cập" });
                }

                lesson.VideoUrl = request.VideoUrl;
                lesson.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Cập nhật video thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // Alias cho UpdateLessonVideo - dùng cho thêm video mới
        [HttpPost]
        public Task<IActionResult> AddLessonVideo([FromBody] UpdateLessonVideoRequest request)
        {
            return UpdateLessonVideo(request);
        }

        // DTO cho update video request
        public class UpdateLessonVideoRequest
        {
            public int LessonId { get; set; }
            public string VideoUrl { get; set; } = string.Empty;
        }

        // POST: Cập nhật tên hiển thị lớp học qua AJAX
        [HttpPost]
        public async Task<IActionResult> UpdateClassTitle([FromBody] UpdateClassTitleRequest request)
        {
            try
            {
                if (!CheckTeacherAccess())
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập!" });
                }

                if (string.IsNullOrWhiteSpace(request.MissionTitle))
                {
                    return Json(new { success = false, message = "Tên hiển thị không được để trống!" });
                }

                var teacherId = GetCurrentTeacherId();
                var classInfo = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == request.ClassId && c.InstructorId == teacherId.Value);

                if (classInfo == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học hoặc bạn không có quyền truy cập!" });
                }

                classInfo.MissionTitle = request.MissionTitle.Trim();
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Cập nhật tên hiển thị thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Class để nhận dữ liệu từ AJAX request
        public class UpdateClassTitleRequest
        {
            public int ClassId { get; set; }
            public string MissionTitle { get; set; } = string.Empty;
        }

        // DTO class cho việc nhận dữ liệu chương từ form
        public class LessonDto
        {
            public int? LessonId { get; set; }  // Null cho chương mới, có giá trị cho chương cũ
            public string Title { get; set; } = null!;
            public string? Content { get; set; }
            public string? VideoUrl { get; set; }
            public int OrderNumber { get; set; }
            public int? Week { get; set; }
            public int? Duration { get; set; }
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
            var assignmentsData = await _context.Assignments
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Class)
                    .ThenInclude(c => c.ClassStudents)
                .Include(a => a.Submissions)
                .Where(a => a.CreatedBy == teacherId.Value && a.DeletedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            // Tạo anonymous object với PendingGrading đã tính sẵn
            var assignments = assignmentsData.Select(a => new {
                AssignmentId = a.AssignmentId,
                Title = a.Title,
                Description = a.Description,
                DueDate = a.DueDate,
                MaxScore = a.MaxScore,
                AttachmentPath = a.AttachmentPath,
                CreatedAt = a.CreatedAt,
                Class = a.Class,
                Submissions = a.Submissions,
                SubmissionCount = a.Submissions?.Count ?? 0,
                TotalStudents = a.Class?.ClassStudents?.Count ?? 0,
                PendingGrading = a.Submissions?.Count(s => s.Status == "Submitted") ?? 0
            }).ToList();

            // Load classes for filter
            var classes = await _context.Classes
                .Include(c => c.Course)
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                .Select(c => new {
                    ClassId = c.ClassId,
                    ClassName = c.Name,
                    CourseName = c.Course.Name
                })
                .ToListAsync();
            ViewBag.Classes = classes;

            // Tính số bài chờ chấm
            ViewBag.PendingGrading = assignments.Sum(a => a.PendingGrading);

            return View(assignments);
        }

        // Tạo bài tập mới
        public async Task<IActionResult> CreateAssignment(int? classId = null)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classes = await _context.Classes
                .Include(c => c.Course)
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                .Select(c => new {
                    ClassId = c.ClassId,
                    ClassName = c.Name,
                    CourseName = c.Course.Name
                })
                .ToListAsync();

            ViewBag.Classes = classes;
            ViewBag.SelectedClassId = classId;
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
                    .Select(c => new {
                        ClassId = c.ClassId,
                        ClassName = c.Name,
                        CourseName = c.Course.Name
                    })
                    .ToListAsync();
                ViewBag.Classes = classes;
                
                return View(assignment);
            }
        }

        // Tổng quan chấm điểm bài tập
        public async Task<IActionResult> GradingOverviewAssignment(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var assignment = await _context.Assignments
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Class)
                    .ThenInclude(c => c.ClassStudents)
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
                .Include(a => a.Class)
                    .ThenInclude(c => c.ClassStudents)
                        .ThenInclude(cs => cs.Student)
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

        // Xuất điểm bài tập ra Excel
        public async Task<IActionResult> ExportSubmissionsToExcel(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var assignment = await _context.Assignments
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Class)
                    .ThenInclude(c => c.ClassStudents)
                        .ThenInclude(cs => cs.Student)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(a => a.AssignmentId == id && a.CreatedBy == teacherId.Value);

            if (assignment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài tập!";
                return RedirectToAction("Assignments");
            }

            // Cấu hình EPPlus License
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Điểm bài tập");

                // Tiêu đề
                worksheet.Cells["A1"].Value = "BẢNG ĐIỂM BÀI TẬP";
                worksheet.Cells["A1:I1"].Merge = true;
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Thông tin bài tập
                worksheet.Cells["A2"].Value = $"Bài tập: {assignment.Title}";
                worksheet.Cells["A2:I2"].Merge = true;
                worksheet.Cells["A2"].Style.Font.Size = 12;
                worksheet.Cells["A2"].Style.Font.Bold = true;

                worksheet.Cells["A3"].Value = $"Môn học: {assignment.Class?.Course?.Name} - Lớp: {assignment.Class?.Name}";
                worksheet.Cells["A3:I3"].Merge = true;

                worksheet.Cells["A4"].Value = $"Điểm tối đa: {assignment.MaxScore}";
                worksheet.Cells["A4:I4"].Merge = true;

                worksheet.Cells["A5"].Value = $"Hạn nộp: {assignment.DueDate:dd/MM/yyyy HH:mm}";
                worksheet.Cells["A5:I5"].Merge = true;

                // Header của bảng
                int row = 7;
                worksheet.Cells[row, 1].Value = "STT";
                worksheet.Cells[row, 2].Value = "Mã SV/GV";
                worksheet.Cells[row, 3].Value = "Họ và tên";
                worksheet.Cells[row, 4].Value = "Email";
                worksheet.Cells[row, 5].Value = "Trạng thái";
                worksheet.Cells[row, 6].Value = "Ngày nộp";
                worksheet.Cells[row, 7].Value = "Điểm";
                worksheet.Cells[row, 8].Value = "Ngày chấm";
                worksheet.Cells[row, 9].Value = "Nhận xét";

                // Style cho header
                using (var range = worksheet.Cells[row, 1, row, 9])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(17, 138, 178));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }

                // Dữ liệu sinh viên
                row = 8;
                int stt = 1;
                var totalStudents = assignment.Class?.ClassStudents?.Count ?? 0;
                var submittedCount = assignment.Submissions?.Count ?? 0;
                decimal totalScore = 0;
                int gradedCount = 0;

                if (assignment.Class?.ClassStudents != null)
                {
                    foreach (var classStudent in assignment.Class.ClassStudents.OrderBy(cs => cs.Student.FullName))
                    {
                        var student = classStudent.Student;
                        var submission = assignment.Submissions?.FirstOrDefault(s => s.StudentId == student.UserId);

                        worksheet.Cells[row, 1].Value = stt++;
                        worksheet.Cells[row, 2].Value = student.MssvMgv ?? "";
                        worksheet.Cells[row, 3].Value = student.FullName ?? "";
                        worksheet.Cells[row, 4].Value = student.Email ?? "";
                        worksheet.Cells[row, 5].Value = submission != null ? (submission.Status == "Graded" ? "Đã chấm" : "Đã nộp") : "Chưa nộp";
                        worksheet.Cells[row, 6].Value = submission?.SubmittedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
                        worksheet.Cells[row, 7].Value = submission?.Score?.ToString() ?? "";
                        worksheet.Cells[row, 8].Value = submission?.GradedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
                        worksheet.Cells[row, 9].Value = submission?.Feedback ?? "";

                        // Tính tổng điểm
                        if (submission?.Score != null)
                        {
                            totalScore += submission.Score.Value;
                            gradedCount++;
                        }

                        // Style cho dòng
                        using (var range = worksheet.Cells[row, 1, row, 9])
                        {
                            range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                        }

                        row++;
                    }
                }

                // Thống kê
                row += 2;
                worksheet.Cells[row, 1].Value = "THỐNG KÊ";
                worksheet.Cells[row, 1].Style.Font.Bold = true;

                row++;
                worksheet.Cells[row, 1].Value = "Tổng số sinh viên:";
                worksheet.Cells[row, 2].Value = totalStudents;

                row++;
                worksheet.Cells[row, 1].Value = "Số sinh viên đã nộp:";
                worksheet.Cells[row, 2].Value = submittedCount;

                row++;
                worksheet.Cells[row, 1].Value = "Số sinh viên đã chấm:";
                worksheet.Cells[row, 2].Value = gradedCount;

                row++;
                worksheet.Cells[row, 1].Value = "Điểm trung bình:";
                worksheet.Cells[row, 2].Value = gradedCount > 0 ? (totalScore / gradedCount).ToString("0.00") : "N/A";

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Đặt độ rộng tối thiểu cho các cột
                worksheet.Column(3).Width = 25; // Họ và tên
                worksheet.Column(4).Width = 30; // Email
                worksheet.Column(9).Width = 40; // Nhận xét

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"DiemBaiTap_{assignment.Title}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        // Giao diện chấm điểm chi tiết cho bài nộp
        public async Task<IActionResult> GradeSubmissionDetail(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            
            // Lấy assignmentId và studentId từ query string
            if (!Request.Query.ContainsKey("assignmentId") || !Request.Query.ContainsKey("studentId"))
            {
                TempData["ErrorMessage"] = "Thiếu thông tin bài tập hoặc sinh viên!";
                return RedirectToAction("Assignments");
            }

            var assignmentId = int.Parse(Request.Query["assignmentId"].ToString());
            var studentId = int.Parse(Request.Query["studentId"].ToString());
            
            var assignment = await _context.Assignments
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Class)
                    .ThenInclude(c => c.ClassStudents)
                        .ThenInclude(cs => cs.Student)
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.CreatedBy == teacherId.Value);

            if (assignment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bài tập!";
                return RedirectToAction("Assignments");
            }

            var student = await _context.Users.FindAsync(studentId);
            if (student == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sinh viên!";
                return RedirectToAction("AssignmentSubmissions", new { id = assignmentId });
            }

            var submission = assignment.Submissions.FirstOrDefault(s => s.StudentId == studentId);
            
            // Lấy tổng số sinh viên và vị trí hiện tại
            var allStudents = assignment.Class.ClassStudents.OrderBy(cs => cs.Student.FullName).ToList();
            var currentIndex = allStudents.FindIndex(cs => cs.StudentId == studentId);
            
            ViewBag.CurrentStudent = student;
            ViewBag.CurrentIndex = currentIndex + 1;
            ViewBag.TotalStudents = allStudents.Count;
            ViewBag.AllStudents = allStudents;
            ViewBag.Submission = submission;

            return View(assignment);
        }

        // Chấm điểm bài tập
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> GradeSubmission([FromBody] GradeSubmissionRequest request)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                
                // Debug log
                System.Diagnostics.Debug.WriteLine($"GradeSubmission - SubmissionId: {request.SubmissionId}, TeacherId: {teacherId}, Score: {request.Score}");
                
                var submission = await _context.Submissions
                    .Include(s => s.Assignment)
                    .Include(s => s.Student)
                    .FirstOrDefaultAsync(s => s.SubmissionId == request.SubmissionId);

                if (submission == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Submission not found with ID: {request.SubmissionId}");
                    return Json(new { success = false, message = "Không tìm thấy bài nộp!" });
                }
                
                // Kiểm tra quyền - chỉ giảng viên tạo bài tập mới được chấm
                if (submission.Assignment.CreatedBy != teacherId.Value)
                {
                    System.Diagnostics.Debug.WriteLine($"Permission denied - Assignment created by: {submission.Assignment.CreatedBy}, Current teacher: {teacherId}");
                    return Json(new { success = false, message = "Bạn không có quyền chấm bài tập này!" });
                }

                submission.Score = request.Score;
                submission.Feedback = request.Feedback;
                submission.Status = "Graded";
                submission.GradedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                
                System.Diagnostics.Debug.WriteLine($"Grade saved successfully for submission {request.SubmissionId}");

                // Tạo thông báo nếu được chọn
                if (request.NotifyStudent)
                {
                    var notification = new Notification
                    {
                        SenderId = teacherId.Value,
                        ReceiverId = submission.StudentId,
                        Title = "Bài tập đã được chấm điểm",
                        Content = $"Bài tập '{submission.Assignment.Title}' của bạn đã được chấm điểm: {request.Score}/{submission.Assignment.MaxScore}",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Chấm điểm thành công!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GradeSubmission: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Model class cho request
        public class GradeSubmissionRequest
        {
            public int SubmissionId { get; set; }
            public decimal Score { get; set; }
            public string? Feedback { get; set; }
            public bool NotifyStudent { get; set; }
        }

        // Lưu điểm và chuyển sang sinh viên tiếp theo
        [HttpPost]
        public async Task<IActionResult> SaveAndNext(int assignmentId, int studentId, decimal? score, string? feedback, bool notifyStudent = true)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                
                // Lấy assignment và submission
                var assignment = await _context.Assignments
                    .Include(a => a.Class)
                        .ThenInclude(c => c.ClassStudents)
                            .ThenInclude(cs => cs.Student)
                    .Include(a => a.Submissions)
                    .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.CreatedBy == teacherId.Value);

                if (assignment == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bài tập!" });
                }

                var submission = assignment.Submissions.FirstOrDefault(s => s.StudentId == studentId);
                
                // Lưu điểm nếu có submission và có điểm
                if (submission != null && score.HasValue)
                {
                    submission.Score = score.Value;
                    submission.Feedback = feedback;
                    submission.Status = "Graded";
                    submission.GradedAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    // Tạo thông báo
                    if (notifyStudent)
                    {
                        var notification = new Notification
                        {
                            SenderId = teacherId.Value,
                            ReceiverId = studentId,
                            Title = "Bài tập đã được chấm điểm",
                            Content = $"Bài tập '{assignment.Title}' của bạn đã được chấm điểm: {score}/{assignment.MaxScore}",
                            IsRead = false,
                            CreatedAt = DateTime.Now
                        };
                        _context.Notifications.Add(notification);
                        await _context.SaveChangesAsync();
                    }
                }

                // Tìm sinh viên tiếp theo
                var allStudents = assignment.Class.ClassStudents.OrderBy(cs => cs.Student.FullName).ToList();
                var currentIndex = allStudents.FindIndex(cs => cs.StudentId == studentId);
                
                if (currentIndex < allStudents.Count - 1)
                {
                    var nextStudent = allStudents[currentIndex + 1];
                    return Json(new { 
                        success = true, 
                        message = "Đã lưu!",
                        nextStudentId = nextStudent.StudentId,
                        hasNext = true
                    });
                }
                else
                {
                    return Json(new { 
                        success = true, 
                        message = "Đã chấm xong tất cả sinh viên!",
                        hasNext = false
                    });
                }
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
            ViewBag.Difficulties = new SelectList(difficulties, "LevelId", "LevelName");

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
                // Lấy inner exception để hiển thị lỗi chi tiết hơn
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["ErrorMessage"] = $"Lỗi khi tạo câu hỏi: {errorMessage}";
                
                var teacherId = GetCurrentTeacherId();
                var myCourses = await _context.Classes
                    .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null)
                    .Select(c => c.Course)
                    .Distinct()
                    .ToListAsync();

                ViewBag.Courses = new SelectList(myCourses, "CourseId", "Name", question.CourseId);
                var difficulties = await _context.DifficultyLevels.ToListAsync();
                ViewBag.Difficulties = new SelectList(difficulties, "LevelId", "LevelName", question.DifficultyId);
                
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
            ViewBag.Difficulties = new SelectList(difficulties, "LevelId", "LevelName", question.DifficultyId);

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

        // GET: Teacher/CreateLesson
        public async Task<IActionResult> CreateLesson(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.Lessons)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            // Calculate next OrderNumber
            var maxOrderNumber = classInfo.Lessons?.Any() == true 
                ? classInfo.Lessons.Max(l => l.OrderNumber) 
                : 0;

            var newLesson = new Lesson
            {
                ClassId = classId,
                OrderNumber = maxOrderNumber + 1,
                Duration = 60 // Default 60 minutes
            };

            ViewBag.ClassInfo = classInfo;
            return View(newLesson);
        }

        // POST: Teacher/CreateLesson
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLesson(Lesson lesson, List<IFormFile>? newFiles)
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
                    TempData["ErrorMessage"] = "Không có quyền truy cập lớp học này!";
                    return RedirectToAction("MyClasses");
                }

                // Handle file uploads
                if (newFiles != null && newFiles.Count > 0)
                {
                    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "lessons");
                    if (!Directory.Exists(uploadPath))
                    {
                        Directory.CreateDirectory(uploadPath);
                    }

                    var lessonFiles = new List<LessonFile>();
                    foreach (var file in newFiles)
                    {
                        if (file.Length > 0)
                        {
                            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                            var filePath = Path.Combine(uploadPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            lessonFiles.Add(new LessonFile
                            {
                                FileName = file.FileName,
                                FilePath = "/uploads/lessons/" + fileName,
                                FileType = Path.GetExtension(file.FileName),
                                FileSize = (int)file.Length,
                                UploadedAt = DateTime.Now
                            });
                        }
                    }

                    lesson.LessonFiles = lessonFiles;
                }

                _context.Lessons.Add(lesson);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo bài học mới thành công!";
                return RedirectToAction("ClassDetails", new { id = lesson.ClassId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi tạo bài học: " + ex.Message;
                var classInfo = await _context.Classes
                    .Include(c => c.Course)
                    .FirstOrDefaultAsync(c => c.ClassId == lesson.ClassId);
                ViewBag.ClassInfo = classInfo;
                return View(lesson);
            }
        }

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
        // XÓA - Đã thay thế bằng Assignments() - Giao diện mới
        /*
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
        */

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

        // Toggle công bố/ẩn bài tập
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublishAssignment(int assignmentId, bool isPublished)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var teacherId = GetCurrentTeacherId();
                var assignment = await _context.Assignments
                    .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.CreatedBy == teacherId.Value);

                if (assignment == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy bài tập!";
                    return RedirectToAction("Assignments");
                }

                assignment.IsPublished = isPublished;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = isPublished ? "Đã công bố bài tập!" : "Đã ẩn bài tập!";
                return RedirectToAction("ClassDetails", new { id = assignment.ClassId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction("Assignments");
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
        public async Task<IActionResult> CreateExam(int classId, int? lessonId = null)
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
            ViewBag.LessonId = lessonId; // Pass lessonId to view
            
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

        // Tổng quan chấm điểm bài kiểm tra
        public async Task<IActionResult> GradingOverviewExam(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var exam = await _context.Exams
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.Class)
                    .ThenInclude(c => c.ClassStudents)
                .Include(e => e.ExamResults)
                    .ThenInclude(er => er.Student)
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.CreatedBy == teacherId.Value);

            if (exam == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đề thi!";
                return RedirectToAction("Exams");
            }

            return View(exam);
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
                .Include(e => e.Class)
                    .ThenInclude(c => c.ClassStudents)
                        .ThenInclude(cs => cs.Student)
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

        // Xuất điểm thi ra Excel
        public async Task<IActionResult> ExportExamResultsToExcel(int examId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var exam = await _context.Exams
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.Class)
                    .ThenInclude(c => c.ClassStudents)
                        .ThenInclude(cs => cs.Student)
                .Include(e => e.ExamResults)
                    .ThenInclude(er => er.Student)
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.CreatedBy == teacherId.Value);

            if (exam == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đề thi!";
                return RedirectToAction("Exams");
            }

            // Cấu hình EPPlus License
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Điểm thi");

                // Tiêu đề
                worksheet.Cells["A1"].Value = "BẢNG ĐIỂM BÀI THI";
                worksheet.Cells["A1:I1"].Merge = true;
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Thông tin bài thi
                worksheet.Cells["A2"].Value = $"Bài thi: {exam.Title}";
                worksheet.Cells["A2:I2"].Merge = true;
                worksheet.Cells["A2"].Style.Font.Size = 12;
                worksheet.Cells["A2"].Style.Font.Bold = true;

                worksheet.Cells["A3"].Value = $"Môn học: {exam.Class?.Course?.Name} - Lớp: {exam.Class?.Name}";
                worksheet.Cells["A3:I3"].Merge = true;

                worksheet.Cells["A4"].Value = $"Thời gian làm bài: {exam.Duration} phút";
                worksheet.Cells["A4:I4"].Merge = true;

                worksheet.Cells["A5"].Value = $"Ngày thi: {exam.StartTime:dd/MM/yyyy HH:mm} - {exam.EndTime:dd/MM/yyyy HH:mm}";
                worksheet.Cells["A5:I5"].Merge = true;

                // Header của bảng
                int row = 7;
                worksheet.Cells[row, 1].Value = "STT";
                worksheet.Cells[row, 2].Value = "Mã SV/GV";
                worksheet.Cells[row, 3].Value = "Họ và tên";
                worksheet.Cells[row, 4].Value = "Email";
                worksheet.Cells[row, 5].Value = "Trạng thái";
                worksheet.Cells[row, 6].Value = "Số lần thử";
                worksheet.Cells[row, 7].Value = "Ngày hoàn thành";
                worksheet.Cells[row, 8].Value = "Điểm";
                worksheet.Cells[row, 9].Value = "Tổng điểm";

                // Style cho header
                using (var range = worksheet.Cells[row, 1, row, 9])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(17, 138, 178));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }

                // Dữ liệu sinh viên
                row = 8;
                int stt = 1;
                var totalStudents = exam.Class?.ClassStudents?.Count ?? 0;
                var submittedCount = exam.ExamResults?.Count ?? 0;
                decimal totalScore = 0;
                int gradedCount = 0;

                if (exam.Class?.ClassStudents != null)
                {
                    foreach (var classStudent in exam.Class.ClassStudents.OrderBy(cs => cs.Student.FullName))
                    {
                        var student = classStudent.Student;
                        var result = exam.ExamResults?.FirstOrDefault(r => r.StudentId == student.UserId);

                        worksheet.Cells[row, 1].Value = stt++;
                        worksheet.Cells[row, 2].Value = student.MssvMgv ?? "";
                        worksheet.Cells[row, 3].Value = student.FullName ?? "";
                        worksheet.Cells[row, 4].Value = student.Email ?? "";
                        
                        if (result != null)
                        {
                            worksheet.Cells[row, 5].Value = result.Status ?? "Đã hoàn thành";
                            worksheet.Cells[row, 6].Value = result.AttemptNumber?.ToString() ?? "1";
                            worksheet.Cells[row, 7].Value = result.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
                            worksheet.Cells[row, 8].Value = result.Score?.ToString("0.00") ?? "";
                            worksheet.Cells[row, 9].Value = result.TotalScore.ToString("0.00");
                            
                            // Tính tổng điểm
                            if (result.Score != null)
                            {
                                totalScore += result.Score.Value;
                                gradedCount++;
                            }
                        }
                        else
                        {
                            worksheet.Cells[row, 5].Value = "Chưa làm bài";
                            worksheet.Cells[row, 6].Value = "";
                            worksheet.Cells[row, 7].Value = "";
                            worksheet.Cells[row, 8].Value = "";
                            worksheet.Cells[row, 9].Value = "";
                        }

                        // Style cho dòng
                        using (var range = worksheet.Cells[row, 1, row, 9])
                        {
                            range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                        }

                        row++;
                    }
                }

                // Thống kê
                row += 2;
                worksheet.Cells[row, 1].Value = "THỐNG KÊ";
                worksheet.Cells[row, 1].Style.Font.Bold = true;

                row++;
                worksheet.Cells[row, 1].Value = "Tổng số sinh viên:";
                worksheet.Cells[row, 2].Value = totalStudents;

                row++;
                worksheet.Cells[row, 1].Value = "Số sinh viên đã làm bài:";
                worksheet.Cells[row, 2].Value = submittedCount;

                row++;
                worksheet.Cells[row, 1].Value = "Số sinh viên có điểm:";
                worksheet.Cells[row, 2].Value = gradedCount;

                row++;
                worksheet.Cells[row, 1].Value = "Điểm trung bình:";
                worksheet.Cells[row, 2].Value = gradedCount > 0 ? (totalScore / gradedCount).ToString("0.00") : "N/A";

                row++;
                worksheet.Cells[row, 1].Value = "Điểm cao nhất:";
                worksheet.Cells[row, 2].Value = gradedCount > 0 ? exam.ExamResults?.Where(r => r.Score != null).Max(r => r.Score)?.ToString("0.00") : "N/A";

                row++;
                worksheet.Cells[row, 1].Value = "Điểm thấp nhất:";
                worksheet.Cells[row, 2].Value = gradedCount > 0 ? exam.ExamResults?.Where(r => r.Score != null).Min(r => r.Score)?.ToString("0.00") : "N/A";

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Đặt độ rộng tối thiểu cho các cột
                worksheet.Column(3).Width = 25; // Họ và tên
                worksheet.Column(4).Width = 30; // Email

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"DiemThi_{exam.Title}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
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

        #region Attendance Management

        // DISABLED: Danh sách các phiên điểm danh của lớp - Xóa theo yêu cầu
        /*
        public async Task<IActionResult> Attendances(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học hoặc bạn không có quyền truy cập!";
                return RedirectToAction("MyClasses");
            }

            var attendances = await _context.Attendances
                .Include(a => a.Lesson)
                .Include(a => a.AttendanceRecords)
                .Where(a => a.ClassId == classId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewBag.ClassInfo = classInfo;
            ViewBag.ClassId = classId;

            return View(attendances);
        }
        */

        // DISABLED: Form tạo phiên điểm danh mới - Xóa theo yêu cầu
        /*
        [HttpGet]
        public async Task<IActionResult> CreateAttendance(int classId, int? lessonId, string? sectionTitle, string? attendanceTitle)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.Lessons.Where(l => l.IsPublished == true))
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            // Xử lý lessonId và sectionTitle nếu được truyền từ modal
            if (lessonId == null && !string.IsNullOrEmpty(sectionTitle))
            {
                // Tìm hoặc tạo lesson với OrderNumber = 0
                var lesson = await _context.Lessons
                    .FirstOrDefaultAsync(l => l.ClassId == classId && l.OrderNumber == 0 && l.Title == sectionTitle);

                if (lesson == null)
                {
                    lesson = new Lesson
                    {
                        ClassId = classId,
                        Title = sectionTitle,
                        Content = "Section cho điểm danh",
                        OrderNumber = 0,
                        Week = 0,
                        CreatedAt = DateTime.Now
                    };
                    _context.Lessons.Add(lesson);
                    await _context.SaveChangesAsync();
                }
                
                lessonId = lesson.LessonId;
            }

            ViewBag.ClassInfo = classInfo;
            ViewBag.Lessons = classInfo.Lessons.OrderBy(l => l.Week).ThenBy(l => l.OrderNumber).ToList();
            ViewBag.SelectedLessonId = lessonId;
            ViewBag.AttendanceTitle = attendanceTitle;

            return View();
        }
        */

        // DISABLED: Xử lý tạo phiên điểm danh - Xóa theo yêu cầu
        /*
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAttendance(int classId, string title, string? description, 
            int? lessonId, DateTime startTime, DateTime endTime, bool allowCodeAttendance, bool autoClose)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            // Tạo mã điểm danh ngẫu nhiên nếu cho phép điểm danh bằng code
            string? attendanceCode = null;
            if (allowCodeAttendance)
            {
                attendanceCode = GenerateAttendanceCode();
            }

            var attendance = new Attendance
            {
                ClassId = classId,
                LessonId = lessonId,
                Title = title,
                Description = description,
                StartTime = startTime,
                EndTime = endTime,
                AttendanceCode = attendanceCode,
                AllowCodeAttendance = allowCodeAttendance,
                AutoClose = autoClose,
                Status = "Open",
                CreatedBy = teacherId.Value,
                CreatedAt = DateTime.Now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            // Tạo bản ghi điểm danh cho tất cả sinh viên trong lớp (mặc định là Vắng)
            foreach (var classStudent in classInfo.ClassStudents)
            {
                var record = new AttendanceRecord
                {
                    AttendanceId = attendance.AttendanceId,
                    StudentId = classStudent.StudentId,
                    Status = "Absent"
                };
                _context.AttendanceRecords.Add(record);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Tạo phiên điểm danh thành công!";
            return RedirectToAction("TakeAttendance", new { id = attendance.AttendanceId });
        }
        */

        // DISABLED: Tạo phiên điểm danh trực tiếp từ modal - Xóa theo yêu cầu
        /*
        [HttpPost]
        public async Task<IActionResult> CreateAttendanceDirect(int classId, string title, string? description, 
            int? lessonId, string? sectionTitle, DateTime? startTime, DateTime? endTime, 
            bool allowCodeAttendance = true, bool autoClose = true)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            // Xử lý lessonId và sectionTitle nếu được truyền từ modal
            if (lessonId == null && !string.IsNullOrEmpty(sectionTitle))
            {
                // Tìm hoặc tạo lesson với OrderNumber = 0
                var lesson = await _context.Lessons
                    .FirstOrDefaultAsync(l => l.ClassId == classId && l.OrderNumber == 0 && l.Title == sectionTitle);

                if (lesson == null)
                {
                    lesson = new Lesson
                    {
                        ClassId = classId,
                        Title = sectionTitle,
                        Content = "Section cho điểm danh",
                        OrderNumber = 0,
                        Week = 0,
                        CreatedAt = DateTime.Now
                    };
                    _context.Lessons.Add(lesson);
                    await _context.SaveChangesAsync();
                }
                
                lessonId = lesson.LessonId;
            }

            // Thiết lập thời gian mặc định nếu không có
            var now = DateTime.Now;
            if (!startTime.HasValue)
            {
                startTime = now;
            }
            if (!endTime.HasValue)
            {
                endTime = startTime.Value.AddHours(2); // Mặc định 2 giờ
            }

            // Tạo mã điểm danh ngẫu nhiên nếu cho phép điểm danh bằng code
            string? attendanceCode = null;
            if (allowCodeAttendance)
            {
                attendanceCode = GenerateAttendanceCode();
            }

            var attendance = new Attendance
            {
                ClassId = classId,
                LessonId = lessonId,
                Title = title,
                Description = description,
                StartTime = startTime.Value,
                EndTime = endTime.Value,
                AttendanceCode = attendanceCode,
                AllowCodeAttendance = allowCodeAttendance,
                AutoClose = autoClose,
                Status = "Open",
                CreatedBy = teacherId.Value,
                CreatedAt = DateTime.Now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            // Tạo bản ghi điểm danh cho tất cả sinh viên trong lớp (mặc định là Vắng)
            foreach (var classStudent in classInfo.ClassStudents)
            {
                var record = new AttendanceRecord
                {
                    AttendanceId = attendance.AttendanceId,
                    StudentId = classStudent.StudentId,
                    Status = "Absent"
                };
                _context.AttendanceRecords.Add(record);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Tạo phiên điểm danh thành công!", attendanceId = attendance.AttendanceId });
        }
        */

        // Quản lý điểm danh cho một section/lesson cụ thể
        public async Task<IActionResult> ManageAttendance(int lessonId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var lesson = await _context.Lessons
                .Include(l => l.Class)
                .FirstOrDefaultAsync(l => l.LessonId == lessonId && l.Class.InstructorId == teacherId);

            if (lesson == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy section!";
                return RedirectToAction("MyClasses");
            }

            // Lấy tất cả attendance của lesson này
            var attendances = await _context.Attendances
                .Include(a => a.AttendanceRecords)
                .Where(a => a.LessonId == lessonId)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            ViewBag.Lesson = lesson;
            ViewBag.ClassInfo = lesson.Class;
            ViewBag.TotalStudents = await _context.ClassStudents
                .Where(cs => cs.ClassId == lesson.ClassId)
                .CountAsync();

            return View(attendances);
        }

        // Tạo phiên điểm danh nhanh - chỉ với tên, chưa cấu hình đầy đủ
        [HttpPost]
        public async Task<IActionResult> CreateAttendanceQuick(int classId, int lessonId, string title)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            // Kiểm tra xem lessonId có hợp lệ không
            if (lessonId == 0)
            {
                return Json(new { success = false, message = "Vui lòng tạo section Điểm danh trước khi thêm phiên điểm danh!" });
            }

            // Kiểm tra section có tồn tại không
            var section = await _context.Lessons
                .FirstOrDefaultAsync(l => l.LessonId == lessonId && l.ClassId == classId);

            if (section == null)
            {
                return Json(new { success = false, message = "Không tìm thấy section!" });
            }

            // Tạo phiên điểm danh với thông tin cơ bản
            var now = DateTime.Now;
            var attendance = new Attendance
            {
                ClassId = classId,
                LessonId = lessonId,
                Title = title,
                Description = "",
                StartTime = now,
                EndTime = now.AddHours(2), // Mặc định 2 tiếng
                Status = "Open",
                AllowCodeAttendance = true,
                AttendanceCode = GenerateRandomCode(6),
                AutoClose = true,
                CreatedBy = teacherId.Value,
                CreatedAt = now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            // Tạo attendance records cho tất cả sinh viên
            foreach (var classStudent in classInfo.ClassStudents)
            {
                var record = new AttendanceRecord
                {
                    AttendanceId = attendance.AttendanceId,
                    StudentId = classStudent.StudentId,
                    Status = "Absent"
                };
                _context.AttendanceRecords.Add(record);
            }

            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = "Tạo phiên điểm danh thành công! Click vào tên để cấu hình chi tiết.", 
                attendanceId = attendance.AttendanceId 
            });
        }

        // Tạo phiên điểm danh với thông tin đầy đủ từ form "Add session"
        [HttpPost]
        public async Task<IActionResult> CreateAttendanceWithDetails(int classId, int lessonId, string title, 
            string? description, DateTime startTime, DateTime endTime, bool allowCodeAttendance, bool autoClose)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.ClassStudents)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            // Tạo phiên điểm danh với thông tin đầy đủ
            var attendance = new Attendance
            {
                ClassId = classId,
                LessonId = lessonId,
                Title = title,
                Description = description ?? "",
                StartTime = startTime,
                EndTime = endTime,
                Status = "Open",
                AllowCodeAttendance = allowCodeAttendance,
                AttendanceCode = allowCodeAttendance ? GenerateRandomCode(6) : "",
                AutoClose = autoClose,
                CreatedBy = teacherId.Value,
                CreatedAt = DateTime.Now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            // Tạo attendance records cho tất cả sinh viên
            foreach (var classStudent in classInfo.ClassStudents)
            {
                var record = new AttendanceRecord
                {
                    AttendanceId = attendance.AttendanceId,
                    StudentId = classStudent.StudentId,
                    Status = "Absent"
                };
                _context.AttendanceRecords.Add(record);
            }

            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = "Tạo session điểm danh thành công!", 
                attendanceId = attendance.AttendanceId 
            });
        }

        // Tạo section chung (không chỉ điểm danh)
        [HttpPost]
        public async Task<IActionResult> CreateSection(int classId, string sectionTitle)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            // Kiểm tra xem section đã tồn tại chưa
            var existingLesson = await _context.Lessons
                .FirstOrDefaultAsync(l => l.ClassId == classId && l.OrderNumber == 0 && l.Title == sectionTitle);

            if (existingLesson != null)
            {
                return Json(new { success = false, message = "Section này đã tồn tại!" });
            }

            // Tạo section mới
            var lesson = new Lesson
            {
                ClassId = classId,
                Title = sectionTitle,
                Content = "Section cho điểm danh",
                OrderNumber = 0,
                Week = 0,
                CreatedAt = DateTime.Now
            };
            
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Tạo section thành công! Bạn có thể thêm nội dung vào section này.", lessonId = lesson.LessonId });
        }

        // Lấy thông tin section điểm danh
        [HttpGet]
        public async Task<IActionResult> GetAttendanceSection(int classId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            // Tìm section "Điểm danh" (OrderNumber = 0 và title chứa "điểm danh")
            var attendanceSection = await _context.Lessons
                .FirstOrDefaultAsync(l => l.ClassId == classId && 
                                         l.OrderNumber == 0 && 
                                         l.Title.ToLower().Contains("điểm danh"));

            if (attendanceSection == null)
            {
                return Json(new { success = false, message = "Không tìm thấy section Điểm danh!" });
            }

            return Json(new { success = true, lessonId = attendanceSection.LessonId });
        }

        // Xóa section
        [HttpPost]
        public async Task<IActionResult> DeleteSection(int lessonId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var teacherId = GetCurrentTeacherId();
            var lesson = await _context.Lessons
                .Include(l => l.Class)
                .FirstOrDefaultAsync(l => l.LessonId == lessonId && l.OrderNumber == 0);

            if (lesson == null)
            {
                return Json(new { success = false, message = "Không tìm thấy section!" });
            }

            // Kiểm tra quyền sở hữu
            if (lesson.Class.InstructorId != teacherId)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa section này!" });
            }

            // Xóa tất cả phiên điểm danh trong section này
            var attendances = await _context.Attendances
                .Where(a => a.LessonId == lessonId)
                .ToListAsync();

            foreach (var attendance in attendances)
            {
                // Xóa các bản ghi điểm danh
                var records = await _context.AttendanceRecords
                    .Where(r => r.AttendanceId == attendance.AttendanceId)
                    .ToListAsync();
                _context.AttendanceRecords.RemoveRange(records);
                
                // Xóa phiên điểm danh
                _context.Attendances.Remove(attendance);
            }

            // Xóa các file trong lesson nếu có
            var lessonFiles = await _context.LessonFiles
                .Where(f => f.LessonId == lessonId)
                .ToListAsync();
            _context.LessonFiles.RemoveRange(lessonFiles);

            // Xóa lesson (section)
            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa section thành công!" });
        }

        // Màn hình điểm danh - Hiển thị danh sách sinh viên
        [HttpGet]
        public async Task<IActionResult> TakeAttendance(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Lesson)
                .Include(a => a.AttendanceRecords)
                    .ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(a => a.AttendanceId == id && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiên điểm danh!";
                return RedirectToAction("MyClasses");
            }

            // Sắp xếp danh sách sinh viên theo MSSV
            attendance.AttendanceRecords = attendance.AttendanceRecords
                .OrderBy(r => r.Student.MssvMgv)
                .ToList();

            return View(attendance);
        }

        // Cập nhật trạng thái điểm danh cho 1 sinh viên
        [HttpPost]
        public async Task<IActionResult> UpdateAttendanceStatus(int recordId, string status, string? note)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var record = await _context.AttendanceRecords
                .Include(r => r.Attendance)
                .FirstOrDefaultAsync(r => r.RecordId == recordId);

            if (record == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bản ghi!" });
            }

            var teacherId = GetCurrentTeacherId();
            if (record.Attendance.CreatedBy != teacherId)
            {
                return Json(new { success = false, message = "Không có quyền!" });
            }

            // Cập nhật trạng thái
            record.Status = status;
            record.Note = note;
            record.UpdatedAt = DateTime.Now;

            if (status == "Present" && record.CheckInTime == null)
            {
                record.CheckInTime = DateTime.Now;
                record.CheckInMethod = "Manual";
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật thành công!" });
        }

        // Điểm danh nhanh tất cả có mặt
        [HttpPost]
        public async Task<IActionResult> MarkAllPresent(int attendanceId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền!" });
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .Include(a => a.AttendanceRecords)
                .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                return Json(new { success = false, message = "Không tìm thấy phiên điểm danh!" });
            }

            var now = DateTime.Now;
            foreach (var record in attendance.AttendanceRecords)
            {
                if (record.Status == "Absent")
                {
                    record.Status = "Present";
                    record.CheckInTime = now;
                    record.CheckInMethod = "Manual";
                    record.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã điểm danh tất cả có mặt!" });
        }

        // Đóng phiên điểm danh
        [HttpPost]
        public async Task<IActionResult> CloseAttendance(int id)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền!" });
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.AttendanceId == id && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                return Json(new { success = false, message = "Không tìm thấy phiên điểm danh!" });
            }

            attendance.Status = "Closed";
            attendance.ClosedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã đóng phiên điểm danh!" });
        }

        // Mở lại phiên điểm danh
        [HttpPost]
        public async Task<IActionResult> ReopenAttendance(int id)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền!" });
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.AttendanceId == id && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                return Json(new { success = false, message = "Không tìm thấy phiên điểm danh!" });
            }

            attendance.Status = "Open";
            attendance.ClosedAt = null;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã mở lại phiên điểm danh!" });
        }

        // Xem thống kê điểm danh
        public async Task<IActionResult> AttendanceStatistics(int classId)
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
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                return RedirectToAction("MyClasses");
            }

            var attendances = await _context.Attendances
                .Include(a => a.AttendanceRecords)
                .Where(a => a.ClassId == classId)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            ViewBag.ClassInfo = classInfo;
            ViewBag.Attendances = attendances;

            return View();
        }

        // Hiển thị QR code để điểm danh
        public async Task<IActionResult> ShowAttendanceQR(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Class.Instructor)
                .Include(a => a.AttendanceRecords)
                .FirstOrDefaultAsync(a => a.AttendanceId == id && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiên điểm danh!";
                return RedirectToAction("MyClasses");
            }

            return View(attendance);
        }

        // Generate QR Code image
        public IActionResult GenerateQRCode(int attendanceId)
        {
            if (!CheckTeacherAccess())
            {
                return BadRequest();
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = _context.Attendances
                .FirstOrDefault(a => a.AttendanceId == attendanceId && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                return NotFound();
            }

            // Create URL for students to scan
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var attendanceUrl = Url.Action("ScanQRAttendance", "Home", new { id = attendanceId }, Request.Scheme);

            // Generate QR code
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(attendanceUrl, QRCodeGenerator.ECCLevel.Q);
                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    byte[] qrCodeImage = qrCode.GetGraphic(20);
                    return File(qrCodeImage, "image/png");
                }
            }
        }

        // Get attendance stats (for auto-refresh)
        public async Task<IActionResult> GetAttendanceStats(int attendanceId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền!" });
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .Include(a => a.AttendanceRecords)
                .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                return Json(new { success = false, message = "Không tìm thấy phiên điểm danh!" });
            }

            var totalCount = attendance.AttendanceRecords.Count;
            var presentCount = attendance.AttendanceRecords.Count(r => r.Status == "Present");

            return Json(new
            {
                success = true,
                totalCount = totalCount,
                presentCount = presentCount,
                status = attendance.Status
            });
        }

        // Xuất điểm danh ra file Excel
        public async Task<IActionResult> ExportAttendanceToExcel(int attendanceId)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            
            var attendance = await _context.Attendances
                .Include(a => a.Class)
                    .ThenInclude(c => c.Course)
                .Include(a => a.Class.Instructor)
                .Include(a => a.AttendanceRecords)
                    .ThenInclude(ar => ar.Student)
                .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.Class.InstructorId == teacherId);

            if (attendance == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiên điểm danh!";
                return RedirectToAction("MyClasses");
            }

            // Sử dụng EPPlus để tạo file Excel
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Điểm danh");

                // Tiêu đề
                worksheet.Cells["A1"].Value = "BẢNG ĐIỂM DANH";
                worksheet.Cells["A1:F1"].Merge = true;
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Thông tin phiên điểm danh
                worksheet.Cells["A3"].Value = "Lớp học:";
                worksheet.Cells["B3"].Value = attendance.Class?.Name;
                worksheet.Cells["A4"].Value = "Môn học:";
                worksheet.Cells["B4"].Value = attendance.Class?.Course?.Name;
                worksheet.Cells["A5"].Value = "Giảng viên:";
                worksheet.Cells["B5"].Value = attendance.Class?.Instructor?.FullName;
                worksheet.Cells["A6"].Value = "Buổi học:";
                worksheet.Cells["B6"].Value = attendance.Title;
                worksheet.Cells["A7"].Value = "Thời gian:";
                worksheet.Cells["B7"].Value = attendance.StartTime.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cells["B7"].Style.Numberformat.Format = "@"; // Text format

                // Header của bảng
                int row = 9;
                worksheet.Cells[row, 1].Value = "STT";
                worksheet.Cells[row, 2].Value = "Mã sinh viên";
                worksheet.Cells[row, 3].Value = "Họ và tên";
                worksheet.Cells[row, 4].Value = "Trạng thái";
                worksheet.Cells[row, 5].Value = "Thời gian check-in";
                worksheet.Cells[row, 6].Value = "Phương thức";
                worksheet.Cells[row, 7].Value = "Ghi chú";

                // Style cho header
                using (var range = worksheet.Cells[row, 1, row, 7])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Dữ liệu sinh viên
                var records = attendance.AttendanceRecords.OrderBy(r => r.Student?.MssvMgv).ToList();
                int stt = 1;
                foreach (var record in records)
                {
                    row++;
                    worksheet.Cells[row, 1].Value = stt++;
                    worksheet.Cells[row, 2].Value = record.Student?.MssvMgv;
                    worksheet.Cells[row, 3].Value = record.Student?.FullName;
                    
                    string statusText = record.Status switch
                    {
                        "Present" => "Có mặt",
                        "Absent" => "Vắng",
                        "Late" => "Muộn",
                        "Excused" => "Có phép",
                        _ => record.Status
                    };
                    worksheet.Cells[row, 4].Value = statusText;
                    
                    worksheet.Cells[row, 5].Value = record.CheckInTime?.ToString("HH:mm:ss");
                    worksheet.Cells[row, 5].Style.Numberformat.Format = "@";
                    
                    worksheet.Cells[row, 6].Value = record.CheckInMethod ?? "";
                    worksheet.Cells[row, 7].Value = record.Note ?? "";

                    // Border cho từng dòng
                    using (var range = worksheet.Cells[row, 1, row, 7])
                    {
                        range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }
                }

                // Thống kê
                row += 2;
                var totalStudents = records.Count;
                var presentCount = records.Count(r => r.Status == "Present");
                var absentCount = records.Count(r => r.Status == "Absent");
                var lateCount = records.Count(r => r.Status == "Late");
                var excusedCount = records.Count(r => r.Status == "Excused");

                worksheet.Cells[row, 1].Value = "THỐNG KÊ:";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;
                worksheet.Cells[row, 1].Value = "Tổng số sinh viên:";
                worksheet.Cells[row, 2].Value = totalStudents;
                row++;
                worksheet.Cells[row, 1].Value = "Có mặt:";
                worksheet.Cells[row, 2].Value = presentCount;
                worksheet.Cells[row, 3].Value = totalStudents > 0 ? $"({(presentCount * 100.0 / totalStudents):F1}%)" : "";
                row++;
                worksheet.Cells[row, 1].Value = "Vắng:";
                worksheet.Cells[row, 2].Value = absentCount;
                worksheet.Cells[row, 3].Value = totalStudents > 0 ? $"({(absentCount * 100.0 / totalStudents):F1}%)" : "";
                row++;
                worksheet.Cells[row, 1].Value = "Muộn:";
                worksheet.Cells[row, 2].Value = lateCount;
                worksheet.Cells[row, 3].Value = totalStudents > 0 ? $"({(lateCount * 100.0 / totalStudents):F1}%)" : "";
                row++;
                worksheet.Cells[row, 1].Value = "Có phép:";
                worksheet.Cells[row, 2].Value = excusedCount;
                worksheet.Cells[row, 3].Value = totalStudents > 0 ? $"({(excusedCount * 100.0 / totalStudents):F1}%)" : "";

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Tạo file và trả về
                var fileName = $"DiemDanh_{attendance.Class?.Name}_{attendance.StartTime:yyyyMMdd_HHmm}.xlsx";
                var stream = new MemoryStream(package.GetAsByteArray());
                
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        // Hàm tạo mã điểm danh ngẫu nhiên (6 ký tự)
        private string GenerateAttendanceCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Chỉnh sửa phiên điểm danh
        [HttpGet]
        public async Task<IActionResult> EditAttendance(int id)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .Include(a => a.Class)
                .Include(a => a.Lesson)
                .FirstOrDefaultAsync(a => a.AttendanceId == id && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiên điểm danh!";
                return RedirectToAction("MyClasses");
            }

            var lessons = await _context.Lessons
                .Where(l => l.ClassId == attendance.ClassId && l.DeletedAt == null)
                .OrderBy(l => l.OrderNumber)
                .ToListAsync();

            ViewBag.ClassInfo = attendance.Class;
            ViewBag.Lessons = lessons;
            return View(attendance);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAttendance(int attendanceId, string title, string? description, 
            int? lessonId, DateTime startTime, DateTime endTime, bool allowCodeAttendance, bool autoClose)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .Include(a => a.Class)
                .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiên điểm danh!";
                return RedirectToAction("MyClasses");
            }

            // Cập nhật thông tin
            attendance.Title = title;
            attendance.Description = description;
            attendance.LessonId = lessonId;
            attendance.StartTime = startTime;
            attendance.EndTime = endTime;
            attendance.AllowCodeAttendance = allowCodeAttendance;
            attendance.AutoClose = autoClose;

            // Nếu tắt điểm danh bằng mã, xóa mã
            if (!allowCodeAttendance)
            {
                attendance.AttendanceCode = null;
            }
            // Nếu bật điểm danh bằng mã mà chưa có mã, tạo mã mới
            else if (string.IsNullOrEmpty(attendance.AttendanceCode))
            {
                attendance.AttendanceCode = GenerateAttendanceCode();
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật phiên điểm danh thành công!";
            return RedirectToAction("TakeAttendance", new { id = attendanceId });
        }

        // Xóa phiên điểm danh
        [HttpPost]
        public async Task<IActionResult> DeleteAttendance(int id)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            var teacherId = GetCurrentTeacherId();
            var attendance = await _context.Attendances
                .Include(a => a.AttendanceRecords)
                .FirstOrDefaultAsync(a => a.AttendanceId == id && a.CreatedBy == teacherId);

            if (attendance == null)
            {
                return Json(new { success = false, message = "Không tìm thấy phiên điểm danh!" });
            }

            // Xóa tất cả bản ghi điểm danh
            _context.AttendanceRecords.RemoveRange(attendance.AttendanceRecords);
            
            // Xóa phiên điểm danh
            _context.Attendances.Remove(attendance);
            
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa phiên điểm danh thành công!" });
        }

        #endregion

        #region Quản lý Slide/Tài liệu

        // Thêm slide bài giảng
        [HttpPost]
        public async Task<IActionResult> AddSlide(int classId, int? lessonId, string url, string title, string sectionTitle)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            var teacherId = GetCurrentTeacherId();

            // Kiểm tra quyền của giảng viên đối với lớp học
            var classItem = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId && c.DeletedAt == null);

            if (classItem == null)
            {
                return Json(new { success = false, message = "Bạn không có quyền quản lý lớp học này!" });
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return Json(new { success = false, message = "URL không được để trống!" });
            }

            // Sử dụng tiêu đề section từ input, nếu không có thì dùng "Tài liệu chung"
            var lessonTitle = string.IsNullOrWhiteSpace(sectionTitle) ? "Tài liệu chung" : sectionTitle;

            // Nếu có lessonId, thêm vào LessonFiles
            if (lessonId.HasValue && lessonId.Value > 0)
            {
                var lesson = await _context.Lessons
                    .FirstOrDefaultAsync(l => l.LessonId == lessonId.Value && l.ClassId == classId);

                if (lesson == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy chương/bài học!" });
                }

                var lessonFile = new LessonFile
                {
                    LessonId = lessonId.Value,
                    FileName = title ?? url,
                    FilePath = url,
                    FileType = "url",
                    Mimetype = "text/uri-list",
                    Description = "Slide bài giảng",
                    UploadedAt = DateTime.Now
                };

                _context.LessonFiles.Add(lessonFile);
            }
            else
            {
                // Tìm lesson với tiêu đề CỤ THỂ do người dùng nhập
                var generalLesson = await _context.Lessons
                    .FirstOrDefaultAsync(l => l.ClassId == classId && l.OrderNumber == 0 && l.Title == lessonTitle);

                if (generalLesson == null)
                {
                    // Tạo lesson mới với tiêu đề tùy chỉnh
                    generalLesson = new Lesson
                    {
                        ClassId = classId,
                        Title = lessonTitle,
                        Content = "Các tài liệu và slide không thuộc chương cụ thể nào",
                        OrderNumber = 0,
                        Week = 0,
                        CreatedAt = DateTime.Now
                    };
                    _context.Lessons.Add(generalLesson);
                    await _context.SaveChangesAsync();
                }

                var lessonFile = new LessonFile
                {
                    LessonId = generalLesson.LessonId,
                    FileName = title ?? url,
                    FilePath = url,
                    FileType = "url",
                    Mimetype = "text/uri-list",
                    Description = "Slide bài giảng",
                    UploadedAt = DateTime.Now
                };

                _context.LessonFiles.Add(lessonFile);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã thêm slide bài giảng thành công!" });
        }

        // Sửa tài liệu
        [HttpPost]
        public async Task<IActionResult> EditFile(int fileId, string title, string url)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            var teacherId = GetCurrentTeacherId();

            var file = await _context.LessonFiles
                .Include(f => f.Lesson)
                    .ThenInclude(l => l.Class)
                .FirstOrDefaultAsync(f => f.FileId == fileId);

            if (file == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài liệu!" });
            }

            // Kiểm tra quyền của giảng viên đối với lớp học
            if (file.Lesson.Class.InstructorId != teacherId)
            {
                return Json(new { success = false, message = "Bạn không có quyền sửa tài liệu này!" });
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                return Json(new { success = false, message = "Tiêu đề và URL không được để trống!" });
            }

            file.FileName = title;
            file.FilePath = url;
            file.UploadedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã cập nhật tài liệu thành công!" });
        }

        // Xóa tài liệu
        [HttpPost]
        public async Task<IActionResult> DeleteFile(int id)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            var teacherId = GetCurrentTeacherId();

            var file = await _context.LessonFiles
                .Include(f => f.Lesson)
                    .ThenInclude(l => l.Class)
                .FirstOrDefaultAsync(f => f.FileId == id);

            if (file == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài liệu!" });
            }

            // Kiểm tra quyền của giảng viên đối với lớp học
            if (file.Lesson.Class.InstructorId != teacherId)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa tài liệu này!" });
            }

            _context.LessonFiles.Remove(file);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa tài liệu thành công!" });
        }

        // Cập nhật tiêu đề lesson (cho section tài liệu)
        [HttpPost]
        public async Task<IActionResult> UpdateLessonTitle(int lessonId, string title)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            var teacherId = GetCurrentTeacherId();

            var lesson = await _context.Lessons
                .Include(l => l.Class)
                .FirstOrDefaultAsync(l => l.LessonId == lessonId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lesson!" });
            }

            // Kiểm tra quyền của giảng viên đối với lớp học
            if (lesson.Class.InstructorId != teacherId)
            {
                return Json(new { success = false, message = "Bạn không có quyền sửa lesson này!" });
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { success = false, message = "Tiêu đề không được để trống!" });
            }

            lesson.Title = title;
            lesson.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã cập nhật tiêu đề thành công!" });
        }

        // Lấy nội dung lesson để chỉnh sửa
        [HttpGet]
        public async Task<IActionResult> GetLessonContent(int lessonId)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            var teacherId = GetCurrentTeacherId();

            var lesson = await _context.Lessons
                .Include(l => l.Class)
                .FirstOrDefaultAsync(l => l.LessonId == lessonId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "Không tìm thấy chương học!" });
            }

            // Kiểm tra quyền của giảng viên đối với lớp học
            if (lesson.Class.InstructorId != teacherId)
            {
                return Json(new { success = false, message = "Bạn không có quyền truy cập chương học này!" });
            }

            return Json(new { success = true, content = lesson.Content ?? "" });
        }

        // Cập nhật nội dung lesson
        [HttpPost]
        public async Task<IActionResult> UpdateLessonContent(int lessonId, string content)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            var teacherId = GetCurrentTeacherId();

            var lesson = await _context.Lessons
                .Include(l => l.Class)
                .FirstOrDefaultAsync(l => l.LessonId == lessonId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "Không tìm thấy chương học!" });
            }

            // Kiểm tra quyền của giảng viên đối với lớp học
            if (lesson.Class.InstructorId != teacherId)
            {
                return Json(new { success = false, message = "Bạn không có quyền sửa chương học này!" });
            }

            lesson.Content = content;
            lesson.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã cập nhật nội dung chương thành công!" });
        }

        // Cập nhật tiêu đề section nội dung chính
        [HttpPost]
        public async Task<IActionResult> UpdateMissionTitle(int classId, string title)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            var teacherId = GetCurrentTeacherId();

            var classInfo = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (classInfo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            // Kiểm tra quyền của giảng viên đối với lớp học
            if (classInfo.InstructorId != teacherId)
            {
                return Json(new { success = false, message = "Bạn không có quyền sửa lớp học này!" });
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { success = false, message = "Tiêu đề không được để trống!" });
            }

            classInfo.MissionTitle = title;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã cập nhật tiêu đề thành công!" });
        }

        #endregion

        #region 7. Thời khóa biểu - Schedule

        // Hiển thị thời khóa biểu của giảng viên
        public async Task<IActionResult> MySchedule(int? semesterId, int? weekNumber)
        {
            if (!CheckTeacherAccess())
            {
                return RedirectToAction("Login", "Home");
            }

            var teacherId = GetCurrentTeacherId();
            if (teacherId == null)
            {
                TempData["ErrorMessage"] = "Không xác định được thông tin giảng viên!";
                return RedirectToAction("Index");
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

            // Tạo chuỗi semester để lọc (ví dụ: "Học kỳ 1 - Năm học 2025 - 2026")
            var semesterFilter = currentSemester != null 
                ? $"Học kỳ {currentSemester.SemesterNumber} - Năm học {currentSemester.AcademicYear}"
                : null;

            // Lấy tất cả lịch học của các lớp mà giảng viên đang giảng dạy (lọc theo học kỳ nếu có)
            var schedulesQuery = _context.Schedules
                .Include(s => s.Class)
                    .ThenInclude(c => c.Course)
                .Include(s => s.Class)
                    .ThenInclude(c => c.Instructor)
                .Include(s => s.Lesson)
                .Where(s => s.Class.InstructorId == teacherId.Value && s.Class.DeletedAt == null);

            if (!string.IsNullOrEmpty(semesterFilter))
            {
                schedulesQuery = schedulesQuery.Where(s => s.Class.Semester == semesterFilter);
            }

            var schedules = await schedulesQuery
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            // Lấy thông tin lớp học (có thể có lớp không có schedule riêng lẻ, lọc theo học kỳ)
            var classesQuery = _context.Classes
                .Include(c => c.Course)
                .Include(c => c.Instructor)
                .Where(c => c.InstructorId == teacherId.Value && c.DeletedAt == null && c.IsActive == true);

            if (!string.IsNullOrEmpty(semesterFilter))
            {
                classesQuery = classesQuery.Where(c => c.Semester == semesterFilter);
            }

            var classes = await classesQuery.ToListAsync();

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
                // Mỗi tiết ~ 50 phút
                return (int)Math.Ceiling(totalMinutes / 50.0);
            }

            // Tạo dictionary để nhóm theo ngày trong tuần
            var scheduleByDay = new Dictionary<string, List<dynamic>>();
            var daysOfWeek = new[] { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật" };
            
            foreach (var day in daysOfWeek)
            {
                scheduleByDay[day] = new List<dynamic>();
            }

            // Thêm các schedule từ bảng Schedules
            foreach (var schedule in schedules)
            {
                // Kiểm tra xem lớp học có diễn ra trong tuần này không
                if (weekStartFilter.HasValue && weekEndFilter.HasValue && schedule.Class.StartDate.HasValue)
                {
                    var classStartDate = schedule.Class.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                    
                    // Tính số tuần cần học dựa trên số tiết và thời gian mỗi buổi
                    DateTime calculatedEndDate;
                    if (schedule.Class.EndDate.HasValue)
                    {
                        calculatedEndDate = schedule.Class.EndDate.Value.ToDateTime(TimeOnly.MinValue);
                    }
                    else if (schedule.Class.Credits.HasValue && schedule.StartTime != null && schedule.EndTime != null)
                    {
                        // Tính số tiết mỗi buổi
                        int sessionsPerClass = CalculateSessionsFromTime(schedule.StartTime, schedule.EndTime);
                        
                        if (sessionsPerClass > 0)
                        {
                            // Tính số buổi cần học
                            int totalSessions = schedule.Class.Credits.Value;
                            int weeksNeeded = (int)Math.Ceiling((double)totalSessions / sessionsPerClass);
                            
                            // Tính ngày kết thúc thực tế
                            calculatedEndDate = classStartDate.AddDays(weeksNeeded * 7);
                        }
                        else
                        {
                            // Nếu không tính được, sử dụng toàn bộ học kỳ
                            calculatedEndDate = weekEndFilter.Value;
                        }
                    }
                    else
                    {
                        // Nếu không có thông tin, sử dụng toàn bộ học kỳ
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
                        ClassCode = schedule.Class.Code,
                        ClassName = schedule.Class.Name,
                        CourseName = schedule.Class.Course.Name,
                        StartTime = schedule.StartTime.ToString("HH:mm"),
                        EndTime = schedule.EndTime.ToString("HH:mm"),
                        Room = schedule.Room ?? "Chưa xác định",
                        LessonTitle = schedule.Lesson?.Title ?? "",
                        Status = schedule.Status ?? "Scheduled",
                        ClassId = schedule.ClassId,
                        TeacherName = schedule.Class.Instructor?.FullName ?? "Chưa xác định"
                    });
                }
            }

            // Thêm các lịch từ thông tin trong Class (nếu có DayOfWeek, StartTime, EndTime)
            foreach (var cls in classes)
            {
                if (!string.IsNullOrEmpty(cls.DayOfWeek) && cls.StartTime.HasValue && cls.EndTime.HasValue)
                {
                    // Kiểm tra xem lớp học có diễn ra trong tuần này không
                    if (weekStartFilter.HasValue && weekEndFilter.HasValue && cls.StartDate.HasValue)
                    {
                        var classStartDate = cls.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                        
                        // Tính số tuần cần học dựa trên số tiết và thời gian mỗi buổi
                        DateTime calculatedEndDate;
                        if (cls.EndDate.HasValue)
                        {
                            calculatedEndDate = cls.EndDate.Value.ToDateTime(TimeOnly.MinValue);
                        }
                        else if (cls.Credits.HasValue)
                        {
                            // Tính số tiết mỗi buổi
                            int sessionsPerClass = CalculateSessionsFromTime(cls.StartTime.Value, cls.EndTime.Value);
                            
                            if (sessionsPerClass > 0)
                            {
                                // Tính số buổi cần học
                                int totalSessions = cls.Credits.Value;
                                int weeksNeeded = (int)Math.Ceiling((double)totalSessions / sessionsPerClass);
                                
                                // Tính ngày kết thúc thực tế
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

                    // Kiểm tra xem đã có schedule này chưa (tránh trùng lặp)
                    var existingSchedule = schedules.FirstOrDefault(s => 
                        s.ClassId == cls.ClassId && 
                        s.DayOfWeek == cls.DayOfWeek);

                    if (existingSchedule == null)
                    {
                        var dayName = cls.DayOfWeek;
                        if (scheduleByDay.ContainsKey(dayName))
                        {
                            scheduleByDay[dayName].Add(new
                            {
                                ClassCode = cls.Code,
                                ClassName = cls.Name,
                                CourseName = cls.Course.Name,
                                StartTime = cls.StartTime.Value.ToString("HH:mm"),
                                EndTime = cls.EndTime.Value.ToString("HH:mm"),
                                Room = cls.Room ?? "Chưa xác định",
                                LessonTitle = "",
                                Status = "Scheduled",
                                ClassId = cls.ClassId,
                                TeacherName = cls.Instructor?.FullName ?? "Chưa xác định"
                            });
                        }
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
            
            return View();
        }

        #endregion

        #region Grade Management

        // GET: Quản lý điểm của lớp học
        public async Task<IActionResult> GradeManagement(int id)
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
                .Include(c => c.Assignments.Where(a => a.DeletedAt == null))
                    .ThenInclude(a => a.Submissions)
                .Include(c => c.Exams.Where(e => e.DeletedAt == null))
                    .ThenInclude(e => e.ExamResults)
                .FirstOrDefaultAsync(c => c.ClassId == id && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học hoặc bạn không có quyền truy cập!";
                return RedirectToAction("MyClasses");
            }

            // Tính điểm cho từng sinh viên
            var studentGrades = new List<dynamic>();
            foreach (var classStudent in classInfo.ClassStudents.Where(cs => cs.Status == "Active" || cs.Status == "Approved"))
            {
                var studentId = classStudent.StudentId;
                
                // Điểm bài tập
                var assignmentScores = new List<decimal?>();
                decimal? totalAssignmentScore = 0;
                decimal? maxAssignmentScore = 0;
                
                foreach (var assignment in classInfo.Assignments.Where(a => a.IsPublished == true))
                {
                    var submission = assignment.Submissions.FirstOrDefault(s => s.StudentId == studentId);
                    assignmentScores.Add(submission?.Score);
                    totalAssignmentScore += submission?.Score ?? 0;
                    maxAssignmentScore += assignment.MaxScore;
                }

                // Điểm thi
                var examScores = new List<decimal?>();
                decimal? totalExamScore = 0;
                decimal? maxExamScore = 0;
                
                foreach (var exam in classInfo.Exams.Where(e => e.IsPublished == true))
                {
                    var examResult = exam.ExamResults.FirstOrDefault(er => er.StudentId == studentId);
                    examScores.Add(examResult?.Score);
                    totalExamScore += examResult?.Score ?? 0;
                    maxExamScore += exam.MaxScore;
                }

                // Lấy điểm cuối kỳ
                var studentGrade = await _context.StudentGrades
                    .FirstOrDefaultAsync(sg => sg.StudentId == studentId && sg.ClassId == id);

                studentGrades.Add(new
                {
                    StudentId = studentId,
                    StudentCode = classStudent.Student.MssvMgv,
                    StudentName = classStudent.Student.FullName,
                    AssignmentScores = assignmentScores,
                    TotalAssignmentScore = totalAssignmentScore,
                    MaxAssignmentScore = maxAssignmentScore,
                    AssignmentAverage = maxAssignmentScore > 0 ? Math.Round((totalAssignmentScore.Value / maxAssignmentScore.Value) * 10, 2) : (decimal?)null,
                    ExamScores = examScores,
                    TotalExamScore = totalExamScore,
                    MaxExamScore = maxExamScore,
                    ExamAverage = maxExamScore > 0 ? Math.Round((totalExamScore.Value / maxExamScore.Value) * 10, 2) : (decimal?)null,
                    FinalScore = studentGrade?.FinalScore,
                    GradeLetter = studentGrade?.GradeLetter,
                    Status = studentGrade?.Status
                });
            }

            ViewBag.ClassInfo = classInfo;
            ViewBag.Assignments = classInfo.Assignments.Where(a => a.IsPublished == true).OrderBy(a => a.DueDate).ToList();
            ViewBag.Exams = classInfo.Exams.Where(e => e.IsPublished == true).OrderBy(e => e.StartTime).ToList();
            ViewBag.StudentGrades = studentGrades;

            return View();
        }

        // POST: Cập nhật điểm cuối kỳ cho sinh viên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFinalGrade(int classId, int studentId, decimal? finalScore, string? gradeLetter)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            // Kiểm tra điểm hợp lệ (0-10)
            if (finalScore.HasValue && (finalScore < 0 || finalScore > 10))
            {
                return Json(new { success = false, message = "Điểm phải trong khoảng 0-10!" });
            }

            // Tự động tính xếp loại nếu không nhập
            if (finalScore.HasValue && string.IsNullOrEmpty(gradeLetter))
            {
                if (finalScore >= 9) gradeLetter = "A+";
                else if (finalScore >= 8.5m) gradeLetter = "A";
                else if (finalScore >= 8) gradeLetter = "B+";
                else if (finalScore >= 7) gradeLetter = "B";
                else if (finalScore >= 6.5m) gradeLetter = "C+";
                else if (finalScore >= 5.5m) gradeLetter = "C";
                else if (finalScore >= 5) gradeLetter = "D+";
                else if (finalScore >= 4) gradeLetter = "D";
                else gradeLetter = "F";
            }

            var existingGrade = await _context.StudentGrades
                .FirstOrDefaultAsync(sg => sg.StudentId == studentId && sg.ClassId == classId);

            if (existingGrade != null)
            {
                existingGrade.FinalScore = finalScore;
                existingGrade.GradeLetter = gradeLetter;
                existingGrade.CalculatedAt = DateTime.Now;
                existingGrade.Status = finalScore.HasValue ? "Completed" : "Pending";
            }
            else
            {
                var newGrade = new StudentGrade
                {
                    StudentId = studentId,
                    ClassId = classId,
                    FinalScore = finalScore,
                    GradeLetter = gradeLetter,
                    CalculatedAt = DateTime.Now,
                    Status = finalScore.HasValue ? "Completed" : "Pending"
                };
                _context.StudentGrades.Add(newGrade);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật điểm thành công!", gradeLetter = gradeLetter });
        }

        // POST: Tính điểm tự động cho toàn bộ lớp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculateAllGrades(int classId, decimal assignmentWeight = 40, decimal examWeight = 60)
        {
            if (!CheckTeacherAccess())
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            var teacherId = GetCurrentTeacherId();
            var classInfo = await _context.Classes
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .Include(c => c.Assignments.Where(a => a.DeletedAt == null && a.IsPublished == true))
                    .ThenInclude(a => a.Submissions)
                .Include(c => c.Exams.Where(e => e.DeletedAt == null && e.IsPublished == true))
                    .ThenInclude(e => e.ExamResults)
                .FirstOrDefaultAsync(c => c.ClassId == classId && c.InstructorId == teacherId.Value);

            if (classInfo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            int updatedCount = 0;

            foreach (var classStudent in classInfo.ClassStudents.Where(cs => cs.Status == "Active" || cs.Status == "Approved"))
            {
                var studentId = classStudent.StudentId;
                
                // Tính điểm bài tập trung bình
                decimal? assignmentAverage = null;
                var assignments = classInfo.Assignments.ToList();
                if (assignments.Any())
                {
                    decimal totalScore = 0;
                    decimal maxScore = 0;
                    foreach (var assignment in assignments)
                    {
                        var submission = assignment.Submissions.FirstOrDefault(s => s.StudentId == studentId);
                        totalScore += submission?.Score ?? 0;
                        maxScore += assignment.MaxScore;
                    }
                    if (maxScore > 0)
                    {
                        assignmentAverage = (totalScore / maxScore) * 10;
                    }
                }

                // Tính điểm thi trung bình
                decimal? examAverage = null;
                var exams = classInfo.Exams.ToList();
                if (exams.Any())
                {
                    decimal totalScore = 0;
                    decimal maxScore = 0;
                    foreach (var exam in exams)
                    {
                        var examResult = exam.ExamResults.FirstOrDefault(er => er.StudentId == studentId);
                        totalScore += examResult?.Score ?? 0;
                        maxScore += exam.MaxScore;
                    }
                    if (maxScore > 0)
                    {
                        examAverage = (totalScore / maxScore) * 10;
                    }
                }

                // Tính điểm cuối kỳ
                decimal? finalScore = null;
                if (assignmentAverage.HasValue || examAverage.HasValue)
                {
                    var assignmentPart = (assignmentAverage ?? 0) * (assignmentWeight / 100);
                    var examPart = (examAverage ?? 0) * (examWeight / 100);
                    finalScore = Math.Round(assignmentPart + examPart, 2);
                }

                // Tính xếp loại
                string? gradeLetter = null;
                if (finalScore.HasValue)
                {
                    if (finalScore >= 9) gradeLetter = "A+";
                    else if (finalScore >= 8.5m) gradeLetter = "A";
                    else if (finalScore >= 8) gradeLetter = "B+";
                    else if (finalScore >= 7) gradeLetter = "B";
                    else if (finalScore >= 6.5m) gradeLetter = "C+";
                    else if (finalScore >= 5.5m) gradeLetter = "C";
                    else if (finalScore >= 5) gradeLetter = "D+";
                    else if (finalScore >= 4) gradeLetter = "D";
                    else gradeLetter = "F";
                }

                // Lưu vào database
                var existingGrade = await _context.StudentGrades
                    .FirstOrDefaultAsync(sg => sg.StudentId == studentId && sg.ClassId == classId);

                if (existingGrade != null)
                {
                    existingGrade.FinalScore = finalScore;
                    existingGrade.GradeLetter = gradeLetter;
                    existingGrade.CalculatedAt = DateTime.Now;
                    existingGrade.Status = finalScore.HasValue ? "Completed" : "Pending";
                }
                else
                {
                    var newGrade = new StudentGrade
                    {
                        StudentId = studentId,
                        ClassId = classId,
                        FinalScore = finalScore,
                        GradeLetter = gradeLetter,
                        CalculatedAt = DateTime.Now,
                        Status = finalScore.HasValue ? "Completed" : "Pending"
                    };
                    _context.StudentGrades.Add(newGrade);
                }

                updatedCount++;
            }

            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = $"Đã tính điểm tự động cho {updatedCount} sinh viên!", 
                count = updatedCount 
            });
        }

        #endregion

        #region Helper Methods

        // Tạo mã ngẫu nhiên cho điểm danh
        private string GenerateRandomCode(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        #endregion

    }
}
