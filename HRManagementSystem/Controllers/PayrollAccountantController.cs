using HR_KD.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR_KD.Controllers
{
    [Authorize(Roles = PayrollAccess.CompanyRoles)]
    public class PayrollAccountantController : Controller
    {
        public IActionResult ByAccountant()
        {
            return View();
        }
    }
}