using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR_KD.Controllers
{
    [Authorize(Roles = "DIRECTOR,EMPLOYEE_MANAGER")]
    public class LeaveManagerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
