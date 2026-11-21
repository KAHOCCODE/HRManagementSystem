using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR_KD.Controllers
{
    public class HolidayController : Controller
    {
        [Authorize(Roles = "EMPLOYEE_MANAGER")]
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult HolidayManagement()
        {
            return View();
        }
        public IActionResult Holiday()
        {
            return View();
        }
    }

}
