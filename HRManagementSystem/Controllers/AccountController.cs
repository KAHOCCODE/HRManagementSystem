using HR_KD.Data;
using HR_KD.Helpers;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BCrypt.Net;
using HR_KD.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace HR_KD.Controllers
{
    public class AccountController : Controller
    {
        private readonly HrDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AccountController(HrDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = _context.TaiKhoans
                .Include(t => t.TaiKhoanQuyenHans)
                .FirstOrDefault(x => x.Username == model.Username);
            if (user == null)
            {
                TempData["Error"] = "Sai tài khoản hoặc mật khẩu";
                return View(model);
            }

            if (PasswordHelper.IsTemporary(user.PasswordHash)
                || model.Password == "123456"
                || !PasswordHelper.Verify(model.Password, user.PasswordSalt, user.PasswordHash))
            {
                TempData["Error"] = "Sai tài khoản hoặc mật khẩu";
                return View(model);
            }

            var nhanVien = _context.NhanViens.FirstOrDefault(nv => nv.MaNv == user.MaNv);
            if (nhanVien == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin nhân viên";
                return View(model);
            }

            // ✅ Load và add nhiều Role Claims
            var userRoles = user.TaiKhoanQuyenHans.Select(q => q.MaQuyenHan).ToList();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("MaNV", user.MaNv.ToString()),
                new Claim("hr:session", PasswordHelper.SessionStamp(user.PasswordHash))
            };
            claims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

            // ✅ Đăng nhập & tạo session
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = model.RememberMe };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            _httpContextAccessor.HttpContext.Session.SetString("HoTen", nhanVien.HoTen);

            // ✅ Log đăng nhập
            var loginLog = new LoginHistory
            {
                LoginId = Guid.NewGuid(),
                UserId = user.MaNv.ToString(),
                Username = user.Username,
                Roles = string.Join(",", userRoles),
                LoginTime = DateTime.Now,
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers["User-Agent"]
            };
            _context.LoginHistories.Add(loginLog);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Activate() => View();

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Activate(string username, string temporaryPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(temporaryPassword)
                || string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 12
                || Encoding.UTF8.GetByteCount(newPassword) > 56 || newPassword == temporaryPassword)
            {
                ModelState.AddModelError("", "Mật khẩu mới cần ít nhất 12 ký tự, tối đa 56 byte và khác mật khẩu tạm.");
                return View();
            }

            var user = await _context.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Username == username);
            if (user == null || !PasswordHelper.VerifyTemporary(temporaryPassword, user.PasswordSalt, user.PasswordHash))
            {
                ModelState.AddModelError("", "Thông tin kích hoạt không hợp lệ hoặc đã hết hạn.");
                return View();
            }

            var salt = PasswordHelper.GenerateRandomKey();
            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword + salt);
            var updated = await _context.TaiKhoans
                .Where(x => x.Username == user.Username && x.PasswordHash == user.PasswordHash)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PasswordSalt, salt)
                    .SetProperty(x => x.PasswordHash, newHash));

            if (updated != 1)
            {
                ModelState.AddModelError("", "Thông tin kích hoạt không còn hiệu lực.");
                return View();
            }

            TempData["Success"] = "Đã đặt mật khẩu. Bạn có thể đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [Authorize(Roles = "DIRECTOR")]
        [HttpGet]
        public IActionResult ResetActivation() => View();

        [Authorize(Roles = "DIRECTOR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetActivation(string username, [FromServices] EmailService emailService,
            [FromServices] ILogger<AccountController> logger)
        {
            var user = await _context.TaiKhoans.Include(x => x.MaNvNavigation)
                .SingleOrDefaultAsync(x => x.Username == username);
            if (user == null || user.Username == User.Identity?.Name || string.IsNullOrWhiteSpace(user.MaNvNavigation.Email))
            {
                ModelState.AddModelError("", "Tài khoản không hợp lệ, chưa có email hoặc đang được sử dụng để quản trị.");
                return View();
            }

            var temporaryPassword = PasswordHelper.CreateTemporaryPassword();
            var salt = PasswordHelper.GenerateRandomKey();
            var hash = PasswordHelper.HashTemporaryPassword(temporaryPassword, salt);
            try
            {
                emailService.SendEmail(user.MaNvNavigation.Email, "Kích hoạt lại tài khoản nhân sự",
                    $"Tài khoản: {System.Net.WebUtility.HtmlEncode(user.Username)}<br>Mật khẩu tạm: {temporaryPassword}<br>Chọn Đặt mật khẩu lần đầu trên trang đăng nhập trong vòng 24 giờ.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được thư kích hoạt lại cho nhân viên {EmployeeId}.", user.MaNv);
                ModelState.AddModelError("", "Không gửi được thư kích hoạt. Kiểm tra cấu hình email.");
                return View();
            }

            var updated = await _context.TaiKhoans
                .Where(x => x.Username == user.Username && x.PasswordHash == user.PasswordHash)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PasswordSalt, salt)
                    .SetProperty(x => x.PasswordHash, hash));
            if (updated != 1)
            {
                ModelState.AddModelError("", "Tài khoản vừa thay đổi. Hãy gửi lại yêu cầu nếu cần.");
                return View();
            }

            TempData["Success"] = "Đã gửi mật khẩu tạm và thu hồi các phiên đăng nhập cũ.";
            return RedirectToAction(nameof(ResetActivation));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _httpContextAccessor.HttpContext.Session.Clear(); // ✅ Xóa toàn bộ Session khi logout
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
