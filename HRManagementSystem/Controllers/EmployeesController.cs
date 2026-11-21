using HR_KD.Authorization;
using System.Data;
using HR_KD.Models;
using HR_KD.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using HR_KD.Data;
using HR_KD.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR_KD.Controllers
{
    public class EmployeesController : Controller
    {
        private const string PersonnelRoles = "DIRECTOR,EMPLOYEE_MANAGER";
        private readonly HrDbContext _context;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(HrDbContext context, ILogger<EmployeesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Danh sách nhân viên
        [Authorize(Roles = PersonnelRoles)]
        public async Task<IActionResult> Index()
        {
            ViewBag.Worksites = await WorksiteOptions();
            return View();
        }
        #endregion

        #region Thêm nhân viên
        [HttpGet]
        [Authorize(Roles = PersonnelRoles)]
        public async Task<IActionResult> Create()
        {
            await LoadProfileChoices();
            ViewBag.Worksites = await WorksiteOptions();
            return View(new NhanVien());
        }
        #endregion

        #region Thiết lập lương
        [Authorize(Roles = PersonnelRoles)]
        public async Task<IActionResult> SetupSalary(int maNv)
        {
            var nhanVien = await _context.NhanViens.FindAsync(maNv);
            if (nhanVien == null)
            {
                return NotFound();
            }

            // Kiểm tra xem nhân viên đã có lương chưa
            var existingSalary = await _context.ThongTinLuongNVs
                .Where(s => s.MaNv == maNv)
                .OrderByDescending(s => s.NgayApDung)
                .FirstOrDefaultAsync();

            if (existingSalary != null)
            {
                return RedirectToAction("Index"); // Hoặc hiển thị thông báo rằng nhân viên đã có lương
            }

            ViewBag.MaNv = maNv;
            return View();
        }
        #endregion

        #region Quản lý hợp đồng
        [Authorize(Roles = PersonnelRoles)]
        public IActionResult EmployeeContracts()
        {
            return View();
        }

        [Authorize(Roles = PersonnelRoles)]
        public async Task<IActionResult> SetupContracts(int maNv)
        {
            var nhanVien = await _context.NhanViens.FindAsync(maNv);
            if (nhanVien == null)
            {
                return NotFound();
            }

            ViewBag.MaNv = maNv;
            return View();
        }
        #endregion

        #region Chỉnh sửa lương
        [Authorize(Roles = PersonnelRoles)]
        public IActionResult EditSalary(int maLuongNV, int maNv)
        {
            ViewData["MaLuongNV"] = maLuongNV;
            ViewData["MaNv"] = maNv;

            var model = new ThongTinLuongNVDTO
            {
                MaLuongNV = maLuongNV,
                MaNv = maNv
            };

            return View(model);
        }
        #endregion

        [HttpGet]
        [Authorize(Roles = PersonnelRoles)]
        public async Task<IActionResult> EditProfile(int id)
        {
            var profile = await _context.NhanViens.AsNoTracking().Where(x => x.MaNv == id)
                .Select(x => new EmployeeProfile
                {
                    Id = x.MaNv, Name = x.HoTen, Email = x.Email, Phone = x.Sdt, Address = x.DiaChi,
                    DepartmentId = x.MaPhongBan, PositionId = x.MaChucVu, AvatarUrl = x.AvatarUrl,
                    ManagerId = _context.EmployeeReportingLines.Where(line => line.EmployeeId == x.MaNv)
                        .Select(line => (int?)line.ManagerId).FirstOrDefault()
                }).SingleOrDefaultAsync();
            if (profile == null)
                return NotFound();

            await LoadProfileChoices(id);
            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = PersonnelRoles)]
        public async Task<IActionResult> EditProfile(int id, EmployeeProfile profile)
        {
            if (profile.Id != id)
                return BadRequest();

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var employee = await _context.NhanViens.FindAsync(id);
            if (employee == null)
                return NotFound();

            var managers = await _context.EmployeeReportingLines.ToDictionaryAsync(x => x.EmployeeId, x => x.ManagerId);
            if (profile.ManagerId is int manager && (!await _context.NhanViens.AnyAsync(x => x.MaNv == manager)
                || HasReportingCycle(id, manager, managers)))
                ModelState.AddModelError(nameof(profile.ManagerId), "Chọn người quản lý hợp lệ và không tạo vòng báo cáo.");
            if (!await _context.PhongBans.AnyAsync(x => x.MaPhongBan == profile.DepartmentId))
                ModelState.AddModelError(nameof(profile.DepartmentId), "Phòng ban không tồn tại.");
            if (!await _context.ChucVus.AnyAsync(x => x.MaChucVu == profile.PositionId))
                ModelState.AddModelError(nameof(profile.PositionId), "Chức vụ không tồn tại.");
            profile.Email = profile.Email?.Trim();
            if (!string.IsNullOrEmpty(profile.Email) && await _context.NhanViens.AnyAsync(x =>
                x.MaNv != id && x.Email != null && x.Email.ToLower() == profile.Email.ToLower()))
                ModelState.AddModelError(nameof(profile.Email), "Email đã được sử dụng.");
            if (profile.Photo != null && !EmployeePhotos.IsValid(profile.Photo))
                ModelState.AddModelError(nameof(profile.Photo), "Chọn ảnh JPG, PNG hoặc WebP tối đa 5 MB.");

            profile.AvatarUrl = employee.AvatarUrl;
            if (!ModelState.IsValid)
            {
                await LoadProfileChoices(id);
                return View(profile);
            }

            var line = await _context.EmployeeReportingLines.FindAsync(id);
            if (profile.ManagerId is int newManager)
            {
                if (line == null)
                    _context.EmployeeReportingLines.Add(new EmployeeReportingLine { EmployeeId = id, ManagerId = newManager });
                else
                    line.ManagerId = newManager;
            }
            else if (line != null)
                _context.EmployeeReportingLines.Remove(line);

            employee.HoTen = profile.Name.Trim();
            employee.Email = profile.Email;
            employee.Sdt = profile.Phone?.Trim();
            employee.DiaChi = profile.Address?.Trim();
            employee.MaPhongBan = profile.DepartmentId;
            employee.MaChucVu = profile.PositionId;

            string? uploaded = null;
            try
            {
                if (profile.Photo != null)
                    employee.AvatarUrl = uploaded = EmployeePhotos.Save(profile.Photo, id);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException)
            {
                EmployeePhotos.DeleteNewUpload(uploaded);
                _logger.LogWarning(ex, "Không lưu được hồ sơ nhân viên {EmployeeId}.", id);
                ModelState.AddModelError("", "Không lưu được hồ sơ. Vui lòng thử lại.");
                await LoadProfileChoices(id);
                return View(profile);
            }

            TempData["ProfileUpdated"] = "Đã lưu hồ sơ nhân viên.";
            return RedirectToAction(nameof(EditProfile), new { id });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Organization(int? worksiteId, [FromServices] IConfiguration configuration)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var worksites = await _context.Worksites.OrderBy(site => site.Name).ThenBy(site => site.Id)
                .Select(site => new OrganizationWorksite(site.Id, site.Name)).ToListAsync();
            var worksiteNames = worksites.ToDictionary(site => site.Id, site => site.Name);
            var members = await _context.NhanViens.AsNoTracking().OrderBy(x => x.HoTen)
                .Select(x => new OrganizationMember
                {
                    Id = x.MaNv, Name = x.HoTen, Position = x.MaChucVuNavigation.TenChucVu,
                    Department = x.MaPhongBanNavigation.TenPhongBan, AvatarUrl = x.AvatarUrl,
                    ManagerId = _context.EmployeeReportingLines.Where(line => line.EmployeeId == x.MaNv)
                        .Select(line => (int?)line.ManagerId).FirstOrDefault(),
                    WorksiteId = _context.WorksiteAssignments.Where(site => site.EmployeeId == x.MaNv && site.EffectiveFrom <= today)
                        .OrderByDescending(site => site.EffectiveFrom).Select(site => (int?)site.WorksiteId).FirstOrDefault()
                }).ToListAsync();
            var byId = members.ToDictionary(x => x.Id);
            var managers = members.Where(x => x.ManagerId.HasValue).ToDictionary(x => x.Id, x => x.ManagerId!.Value);
            var roots = new List<OrganizationMember>();

            foreach (var member in members)
            {
                if (member.WorksiteId is int memberWorksiteId)
                    member.Worksite = worksiteNames.GetValueOrDefault(memberWorksiteId);

                if (member.ManagerId is int id && byId.TryGetValue(id, out var manager)
                    && !HasReportingCycle(member.Id, id, managers))
                    manager.Reports.Add(member);
                else
                {
                    member.ManagerId = null;
                    roots.Add(member);
                }
            }

            var company = configuration["Organization:Name"] ??
                (configuration.GetValue<bool>("Demo:Enabled") ? DemoRoster.CompanyName : "Công ty");
            var selectedWorksiteId = worksiteId is int requestedWorksiteId && worksiteNames.ContainsKey(requestedWorksiteId) ? worksiteId : null;
            return View(new OrganizationChartPage(company, members.Count, roots, worksites, selectedWorksiteId));
        }

        private static bool HasReportingCycle(int employeeId, int managerId, IReadOnlyDictionary<int, int> managers)
        {
            var visited = new HashSet<int> { employeeId };
            while (visited.Add(managerId))
            {
                if (!managers.TryGetValue(managerId, out managerId))
                    return false;
            }
            return true;
        }

        private async Task<SelectList> WorksiteOptions() =>
            new(await _context.Worksites.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(), "Id", "Name");

        private async Task LoadProfileChoices(int? employeeId = null)
        {
            ViewBag.Managers = new SelectList(await _context.NhanViens.Where(x => x.MaNv != employeeId)
                .OrderBy(x => x.HoTen).Select(x => new { x.MaNv, x.HoTen }).ToListAsync(), "MaNv", "HoTen");
            ViewBag.Departments = new SelectList(await _context.PhongBans.OrderBy(x => x.TenPhongBan).ToListAsync(),
                "MaPhongBan", "TenPhongBan");
            ViewBag.Positions = new SelectList(await _context.ChucVus.OrderBy(x => x.TenChucVu).ToListAsync(),
                "MaChucVu", "TenChucVu");
        }

        #region Phân quyền
        [Authorize(Roles = "DIRECTOR,EMPLOYEE_MANAGER,LINE_MANAGER")]
        public IActionResult Roles()
        {
            var currentUser = _context.TaiKhoans
                .Include(t => t.MaNvNavigation)
                .ThenInclude(nv => nv.MaPhongBanNavigation)
                .Include(t => t.TaiKhoanQuyenHans)
                .ThenInclude(t => t.QuyenHan)
                .AsNoTracking() // Tối ưu hiệu suất
                .FirstOrDefault(t => t.Username == User.Identity.Name);

            if (currentUser == null) return Unauthorized();

            var currentNv = currentUser.MaNvNavigation;

            bool isDirector = User.IsInRole("DIRECTOR");

            if (isDirector)
            {
                var allEmployees = _context.TaiKhoans
                    .Include(t => t.MaNvNavigation).ThenInclude(nv => nv.MaChucVuNavigation)
                    .Include(t => t.MaNvNavigation).ThenInclude(nv => nv.MaPhongBanNavigation)
                    .Include(t => t.TaiKhoanQuyenHans).ThenInclude(r => r.QuyenHan)
                    .AsNoTracking()
                    .ToList()
                    .GroupBy(t => t.MaNvNavigation.MaPhongBanNavigation.TenPhongBan)
                    .ToDictionary(g => g.Key, g => g.ToList());

                ViewBag.AllRoles = _context.QuyenHans.AsNoTracking().ToList();
                ViewBag.IsDirector = true;
                ViewBag.DepartmentName = null;
                ViewBag.UserRoles = currentUser.TaiKhoanQuyenHans.Select(r => r.MaQuyenHan).ToList();
                return View(allEmployees);
            }
            else
            {
                var directReports = ReportingAccess.DirectReports(_context, currentNv.MaNv);
                var capDuoi = _context.TaiKhoans
                    .Include(t => t.MaNvNavigation).ThenInclude(nv => nv.MaChucVuNavigation)
                    .Include(t => t.MaNvNavigation).ThenInclude(nv => nv.MaPhongBanNavigation)
                    .Include(t => t.TaiKhoanQuyenHans).ThenInclude(r => r.QuyenHan)
                    .Where(t => directReports.Contains(t.MaNv))
                    .AsNoTracking()
                    .ToList();

                ViewBag.AllRoles = _context.QuyenHans.AsNoTracking().ToList();
                ViewBag.IsDirector = false;
                ViewBag.DepartmentName = currentNv.MaPhongBanNavigation.TenPhongBan;
                ViewBag.UserRoles = currentUser.TaiKhoanQuyenHans.Select(r => r.MaQuyenHan).ToList();
                return View(capDuoi);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRECTOR,EMPLOYEE_MANAGER,LINE_MANAGER")]
        public JsonResult UpdateRoles([FromBody] AssignRoleDto? dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Username) || dto.Roles is not { Count: > 0 })
                return Json(new { success = false, message = "Chọn tài khoản và ít nhất một quyền." });

            var requested = dto.Roles.ToHashSet(StringComparer.Ordinal);
            if (requested.Count != _context.QuyenHans.Count(x => requested.Contains(x.MaQuyenHan)))
                return Json(new { success = false, message = "Có quyền không hợp lệ." });

            using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);
            var target = _context.TaiKhoans.Include(x => x.TaiKhoanQuyenHans)
                .SingleOrDefault(x => x.Username == dto.Username);
            var currentId = PayrollAccess.EmployeeId(User);
            if (target == null || currentId == null || target.MaNv == currentId)
                return Json(new { success = false, message = "Không thể thay đổi quyền của tài khoản này." });

            var existing = target.TaiKhoanQuyenHans.Select(x => x.MaQuyenHan).ToHashSet();
            if (!User.IsInRole("DIRECTOR"))
            {
                string[] delegatedRoles = User.IsInRole("EMPLOYEE_MANAGER") ? ["EMPLOYEE", "LINE_MANAGER"] : ["EMPLOYEE"];
                if (!ReportingAccess.DirectReports(_context, currentId).Contains(target.MaNv)
                    || existing.Except(delegatedRoles).Any() || requested.Except(delegatedRoles).Any())
                    return Json(new { success = false, message = "Bạn không có quyền thay đổi vai trò của tài khoản này." });
            }

            _context.TaiKhoanQuyenHans.RemoveRange(target.TaiKhoanQuyenHans.Where(x => !requested.Contains(x.MaQuyenHan)));
            foreach (var role in requested.Except(existing))
                _context.TaiKhoanQuyenHans.Add(new TaiKhoanQuyenHan { Username = target.Username, MaQuyenHan = role });

            _context.SaveChanges();
            transaction.Commit();
            _logger.LogInformation("User {Username} updated roles for {TargetUsername}", User.Identity?.Name, target.Username);
            return Json(new { success = true });
        }
        #endregion
    }

    public class AssignRoleDto
    {
        public string Username { get; set; }
        public List<string> Roles { get; set; }
    }
}

