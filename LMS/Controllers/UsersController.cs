using LMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers
{
    public class UsersController : Controller
    {
        private readonly LmsSystemContext _context;

        public UsersController(LmsSystemContext context)
        {
            _context = context;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Faculty)
                .Include(u => u.Department)
                .Where(u => u.DeletedAt == null)
                .ToListAsync();
            
            return View(users);
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Faculty)
                .Include(u => u.Department)
                .FirstOrDefaultAsync(m => m.UserId == id);
                
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Faculties
        public async Task<IActionResult> Faculties()
        {
            var faculties = await _context.Faculties
                .Include(f => f.Departments.Where(d => d.DeletedAt == null))
                .Include(f => f.Users.Where(u => u.DeletedAt == null))
                .Where(f => f.IsActive == true && f.DeletedAt == null)
                .ToListAsync();
            
            return View(faculties);
        }

        // GET: Departments
        public async Task<IActionResult> Departments()
        {
            var departments = await _context.Departments
                .Include(d => d.Faculty)
                .Include(d => d.Users.Where(u => u.DeletedAt == null))
                .Where(d => d.IsActive == true && d.DeletedAt == null)
                .ToListAsync();
            
            return View(departments);
        }

        // GET: Courses
        public async Task<IActionResult> Courses()
        {
            var courses = await _context.Courses
                .Include(c => c.Faculty)
                .Include(c => c.LeadInstructor)
                .Where(c => c.IsActive == true && c.DeletedAt == null)
                .ToListAsync();
            
            return View(courses);
        }

        // POST: UpdateDepartmentImages
        public async Task<IActionResult> UpdateDepartmentImages()
        {
            try
            {
                // Kiểm tra và thêm cột ImagePath nếu chưa có
                await EnsureImagePathColumnExists();
                // Cập nhật hình ảnh cho các khoa hiện có
                var departments = await _context.Departments.Where(d => d.IsActive == true).ToListAsync();

                foreach (var dept in departments)
                {
                    switch (dept.Code.ToUpper())
                    {
                        case "CNPM":
                        case "HTTT":
                        case "KTMT":
                            dept.ImagePath = "/images/Faculties/01-KTTC.png";
                            break;
                        case "QTKD":
                        case "KT_TC":
                            dept.ImagePath = "/images/Faculties/03-KTCN.png";
                            break;
                        case "ANH_VAN":
                        case "NHAT_NGU":
                            dept.ImagePath = "/images/Faculties/04-NGNG.png";
                            break;
                        default:
                            // Giữ nguyên ImagePath hiện tại nếu có
                            break;
                    }
                }

                // Thêm các khoa mới nếu chưa có
                var existingCodes = departments.Select(d => d.Code.ToUpper()).ToList();

                if (!existingCodes.Contains("KTXD"))
                {
                    _context.Departments.Add(new Department
                    {
                        Code = "KTXD",
                        Name = "Khoa Kỹ thuật Xây dựng",
                        Description = "Chuyên về xây dựng dân dụng và công nghiệp",
                        FacultyId = 1, // Giả sử FacultyId = 1
                        IsActive = true,
                        ImagePath = "/images/Faculties/06-KTXDGT.png",
                        CreatedAt = DateTime.Now
                    });
                }

                if (!existingCodes.Contains("NONG_NGHIEP"))
                {
                    _context.Departments.Add(new Department
                    {
                        Code = "NONG_NGHIEP",
                        Name = "Khoa Nông nghiệp",
                        Description = "Chuyên về nông nghiệp và phát triển nông thôn",
                        FacultyId = 2, // Giả sử FacultyId = 2
                        IsActive = true,
                        ImagePath = "/images/Faculties/07-NN.png",
                        CreatedAt = DateTime.Now
                    });
                }

                if (!existingCodes.Contains("CNVH_TT_DL"))
                {
                    _context.Departments.Add(new Department
                    {
                        Code = "CNVH_TT_DL",
                        Name = "Khoa Công nghệ Văn hóa - Thông tin - Du lịch",
                        Description = "Chuyên về văn hóa, truyền thông và du lịch",
                        FacultyId = 3, // Giả sử FacultyId = 3
                        IsActive = true,
                        ImagePath = "/images/Faculties/09-CNVH-TT-DL.png",
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                
                ViewBag.Message = "Đã cập nhật hình ảnh cho các khoa thành công!";
                ViewBag.MessageType = "success";
            }
            catch (Exception ex)
            {
                ViewBag.Message = $"Lỗi khi cập nhật: {ex.Message}";
                ViewBag.MessageType = "error";
            }

            return RedirectToAction("Departments");
        }

        // POST: UpdateFacultyImages
        public async Task<IActionResult> UpdateFacultyImages()
        {
            try
            {
                // Kiểm tra và thêm cột ImagePath cho Faculty nếu cần
                await EnsureFacultyImagePathColumnExists();

                // Cập nhật hình ảnh cho các khoa hiện có
                var faculties = await _context.Faculties.Where(f => f.IsActive == true).ToListAsync();

                foreach (var faculty in faculties)
                {
                    switch (faculty.Code?.ToUpper())
                    {
                        case "CNTT":
                            faculty.ImagePath = "/images/Faculties/01-KTTC.png";
                            break;
                        case "KINH_TE":
                            faculty.ImagePath = "/images/Faculties/03-KTCN.png";
                            break;
                        case "NGOAI_NGU":
                            faculty.ImagePath = "/images/Faculties/04-NGNG.png";
                            break;
                        case "CO_KHI":
                            faculty.ImagePath = "/images/Faculties/06-KTXDGT.png";
                            break;
                        case "YD":
                            faculty.ImagePath = "/images/Faculties/07-NN.png";
                            break;
                        case "TDTT":
                            faculty.ImagePath = "/images/Faculties/09-CNVH-TT-DL.png";
                            break;
                        default:
                            // Giữ nguyên ImagePath hiện tại nếu có
                            break;
                    }
                }

                await _context.SaveChangesAsync();
                
                ViewBag.Message = "Đã cập nhật hình ảnh cho các khoa thành công!";
                ViewBag.MessageType = "success";
            }
            catch (Exception ex)
            {
                ViewBag.Message = $"Lỗi khi cập nhật: {ex.Message}";
                ViewBag.MessageType = "error";
            }

            return RedirectToAction("Faculties");
        }

        private async Task EnsureImagePathColumnExists()
        {
            try
            {
                // Kiểm tra xem cột ImagePath đã tồn tại chưa
                var sql = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                                   WHERE TABLE_NAME = 'Departments' 
                                   AND COLUMN_NAME = 'ImagePath')
                    BEGIN
                        ALTER TABLE [dbo].[Departments]
                        ADD [ImagePath] [nvarchar](500) NULL;
                    END";
                
                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                // Nếu có lỗi, có thể cột đã tồn tại hoặc có vấn đề khác
                Console.WriteLine($"Error ensuring ImagePath column: {ex.Message}");
            }
        }

        private async Task EnsureFacultyImagePathColumnExists()
        {
            try
            {
                // Kiểm tra xem cột ImagePath đã tồn tại chưa trong bảng Faculties
                var sql = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                                   WHERE TABLE_NAME = 'Faculties' 
                                   AND COLUMN_NAME = 'ImagePath')
                    BEGIN
                        ALTER TABLE [dbo].[Faculties]
                        ADD [ImagePath] [nvarchar](500) NULL;
                    END";
                
                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                // Nếu có lỗi, có thể cột đã tồn tại hoặc có vấn đề khác
                Console.WriteLine($"Error ensuring Faculty ImagePath column: {ex.Message}");
            }
        }
    }
}