using System.Data;
using HR_KD.Data;
using HR_KD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR_KD.Controllers;

[Authorize(Roles = "DIRECTOR")]
[AutoValidateAntiforgeryToken]
public sealed class WorksitesController(HrDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var sites = await db.Worksites.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        return View(sites);
    }

    [HttpGet]
    public IActionResult Create() => View(new Worksite());

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Code,Name,Province,Ward,Address,Region2026,Region2027")] Worksite site)
    {
        if (!ModelState.IsValid)
            return View(site);

        site.Code = site.Code.Trim().ToUpperInvariant();
        site.Name = site.Name.Trim();
        site.Province = site.Province.Trim();
        site.Ward = site.Ward.Trim();
        site.Address = site.Address.Trim();

        if (HcmWageAreas.IsCity(site.Province))
        {
            site.Province = HcmWageAreas.CityName;
            var area = HcmWageAreas.Find(site.Ward);
            if (area == null)
            {
                ModelState.AddModelError(nameof(site.Ward), "Chọn xã, phường hoặc đặc khu TP.HCM trong danh sách.");
                return View(site);
            }

            site.Ward = area.Name;
            site.Region2026 = area.Region2026 ?? site.Region2026;
            site.Region2027 = area.Region2027;
        }
        else if (!site.Region2026.HasValue || !site.Region2027.HasValue)
        {
            ModelState.AddModelError(nameof(site.Region2026), "Đối chiếu phụ lục và nhập vùng lương theo từng năm.");
            return View(site);
        }

        if (await db.Worksites.AnyAsync(x => x.Code == site.Code))
        {
            ModelState.AddModelError(nameof(site.Code), "Mã chi nhánh đã tồn tại.");
            return View(site);
        }

        db.Worksites.Add(site);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var site = await db.Worksites.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return site == null ? NotFound() : View(site);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string name, string address, int? confirmedRegion2026)
    {
        var site = await db.Worksites.FindAsync(id);
        if (site == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120
            || string.IsNullOrWhiteSpace(address) || address.Trim().Length > 255)
        {
            ModelState.AddModelError("", "Tên chi nhánh hoặc địa chỉ không hợp lệ.");
            return View(site);
        }

        if (confirmedRegion2026.HasValue && (site.Region2026.HasValue || confirmedRegion2026 is < 1 or > 4))
        {
            ModelState.AddModelError("", "Chỉ xác nhận vùng năm 2026 khi chưa lưu vùng và giá trị nằm từ I đến IV.");
            return View(site);
        }

        if (!ModelState.IsValid)
            return View(site);

        site.Name = name.Trim();
        site.Address = address.Trim();
        if (confirmedRegion2026.HasValue)
            site.Region2026 = confirmedRegion2026;

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Assign(int? employeeId)
    {
        await LoadAssignmentChoices(employeeId);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Assign(int employeeId, int worksiteId, DateOnly effectiveFrom)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var employee = await db.NhanViens.AsNoTracking().Where(x => x.MaNv == employeeId)
            .Select(x => new { x.NgayVaoLam }).SingleOrDefaultAsync();
        if (effectiveFrom == default
            || employee == null
            || !await db.Worksites.AnyAsync(x => x.Id == worksiteId))
        {
            ModelState.AddModelError("", "Chọn nhân viên, chi nhánh và ngày bắt đầu hợp lệ.");
        }
        else if (employee.NgayVaoLam is DateOnly hireDate && effectiveFrom < hireDate)
        {
            ModelState.AddModelError("", "Ngày bắt đầu tại chi nhánh phải từ ngày vào làm trở đi.");
        }
        else if (await db.WorksiteAssignments.AnyAsync(x => x.EmployeeId == employeeId && x.EffectiveFrom == effectiveFrom))
        {
            ModelState.AddModelError("", "Nhân viên đã có chi nhánh vào ngày này.");
        }
        else if (await db.BangLuongs.AnyAsync(x => x.MaNv == employeeId
            && x.ThangNam >= new DateOnly(effectiveFrom.Year, effectiveFrom.Month, 1)))
        {
            ModelState.AddModelError("", "Mốc này có thể đổi kỳ đã tạo lương. Hãy đối soát bảng lương trước khi gán.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAssignmentChoices(employeeId);
            ViewBag.SelectedWorksiteId = worksiteId;
            ViewBag.EffectiveFrom = effectiveFrom == default ? null : effectiveFrom.ToString("yyyy-MM-dd");
            return View();
        }

        db.WorksiteAssignments.Add(new WorksiteAssignment { EmployeeId = employeeId, WorksiteId = worksiteId, EffectiveFrom = effectiveFrom });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["WorksiteAssignmentSaved"] = "Đã lưu nơi làm việc.";
        return RedirectToAction(nameof(Assign), new { employeeId });
    }

    private async Task LoadAssignmentChoices(int? employeeId)
    {
        ViewBag.EmployeeId = employeeId;
        ViewBag.Employees = await db.NhanViens.AsNoTracking().OrderBy(x => x.HoTen)
            .Select(x => new KeyValuePair<int, string>(x.MaNv, x.HoTen)).ToListAsync();
        ViewBag.Worksites = await db.Worksites.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        ViewBag.History = employeeId.HasValue
            ? await db.WorksiteAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId)
                .Include(x => x.Worksite).OrderByDescending(x => x.EffectiveFrom).ToListAsync()
            : new List<WorksiteAssignment>();
    }
}
