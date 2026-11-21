using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR_KD.Controllers
{
    [Authorize(Roles = "DIRECTOR")]
    public class PayrollDirectorController : Controller
    {
        public IActionResult DirectorReview()
        {
            return View();
        }
    }
}