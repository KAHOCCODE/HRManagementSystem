using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR_KD.Controllers
{
    [Authorize(Roles = "DIRECTOR")]
    public class HolidayManagerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Requests()
        {
            return View();
        }
    }
}
