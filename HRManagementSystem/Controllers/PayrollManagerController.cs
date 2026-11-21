using HR_KD.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR_KD.Controllers
{
    [Authorize(Roles = PayrollAccess.ReviewRoles)]
    public class PayrollManagerController : Controller
    {
        public IActionResult DepartmentReview()
        {
            return View();
        }
    }
}