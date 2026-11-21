using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR_KD.Controllers
{
    public class AttendanceReportController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        [Authorize(Roles = "DIRECTOR")]
        public IActionResult Director()
        {
            return View();
        }
        [Authorize(Roles = "EMPLOYEE_MANAGER")]
        public IActionResult Manager()
        {
            return View();
        }
    }
}
