using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LMS.Controllers
{
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Set ViewBag cho mọi action
            ViewBag.IsLoggedIn = HttpContext.Session.GetString("IsLoggedIn") == "true";
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "";
            ViewBag.UserId = HttpContext.Session.GetInt32("UserId");
            ViewBag.FacultyName = HttpContext.Session.GetString("FacultyName") ?? "";
            ViewBag.FacultyId = HttpContext.Session.GetString("FacultyId") ?? "";
            
            base.OnActionExecuting(context);
        }
    }
}
