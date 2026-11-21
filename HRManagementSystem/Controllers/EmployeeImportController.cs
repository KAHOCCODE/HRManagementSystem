using HR_KD.Data;
using HR_KD.DTOs;
using HR_KD.Helpers;
using HR_KD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace HR_KD.Controllers
{
    [Authorize(Roles = "EMPLOYEE_MANAGER,DIRECTOR")]
    public class EmployeeImportController : Controller
    {
        private readonly HrDbContext _context;
        private readonly ExcelTemplateService _excelService;
        private readonly UsernameGeneratorService _usernameGen;
        private readonly AccountActivationService _activation;
        private readonly ILogger<EmployeeImportController> _logger;

        public EmployeeImportController(
            HrDbContext context,
            ExcelTemplateService excelService,
            UsernameGeneratorService usernameGen, AccountActivationService activation, ILogger<EmployeeImportController> logger)
        {
            _context = context;
            _excelService = excelService;
            _usernameGen = usernameGen;
            _activation = activation;
            _logger = logger;
        }

        public IActionResult Index() => View();

        public IActionResult DownloadTemplate()
        {
            var fileBytes = _excelService.GenerateTemplateWithData();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Template_Import_NhanVien.xlsx");
        }

        #region Upload + Review dữ liệu từ Excel
        [HttpPost("Upload")]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return RedirectToAction("Index");

            using var stream = file.OpenReadStream();
            var result = await _excelService.ParseExcelWithValidation(stream);

            // Gắn lỗi vào từng dòng (dễ render màu)
            foreach (var emp in result.Employees)
            {
                emp.RowIndex = result.Employees.IndexOf(emp) + 2; // để log lại dòng excel nếu cần
                if (result.Errors.TryGetValue(emp.RowIndex, out var error))
                    emp.RowError = error;
            }

            HttpContext.Session.SetString("ImportedEmployees", JsonConvert.SerializeObject(result.Employees));
            HttpContext.Session.SetString("ImportErrors", JsonConvert.SerializeObject(result.Errors));

            return RedirectToAction("Review");
        }
        #endregion

        #region Xem trước dữ liệu
        public IActionResult Review()
        {
            var json = HttpContext.Session.GetString("ImportedEmployees");
            var errJson = HttpContext.Session.GetString("ImportErrors");

            if (string.IsNullOrEmpty(json)) return RedirectToAction("Index");

            var employees = JsonConvert.DeserializeObject<List<ImportNhanVienDto>>(json);
            var errors = JsonConvert.DeserializeObject<Dictionary<int, string>>(errJson ?? "{}");

            ViewBag.Errors = errors;
            ViewBag.ChucVus = _context.ChucVus.ToDictionary(c => c.MaChucVu, c => c.TenChucVu);
            ViewBag.PhongBans = _context.PhongBans.ToDictionary(p => p.MaPhongBan, p => p.TenPhongBan);

            return View(employees);
        }
        #endregion

        #region Lưu hàng loạt
        [HttpPost("/api/EmployeesApi/SaveImportBatch")]
        public IActionResult SaveImportBatch([FromForm] List<ImportNhanVienDto> list)
        {
            var savedCount = 0;
            var activationEmailsFailed = 0;

            foreach (var dto in list)
            {
                try
                {
                    // Skip nếu lỗi
                    if (!string.IsNullOrEmpty(dto.RowError)) continue;

                    // Kiểm tra trùng
                    if (_context.TaiKhoans.Any(x => x.Username == dto.Sdt)) continue;
                    if (_context.NhanViens.Any(x => x.Email == dto.Email)) continue;

                    using var transaction = _context.Database.BeginTransaction();
                    var nv = new NhanVien
                    {
                        HoTen = dto.HoTen,
                        NgaySinh = DateOnly.FromDateTime(dto.NgaySinh),
                        GioiTinh = dto.GioiTinh,
                        DiaChi = "", // Tùy nhập sau
                        Sdt = dto.Sdt,
                        Email = dto.Email,
                        TrinhDoHocVan = dto.TrinhDoHocVan,
                        MaPhongBan = dto.MaPhongBan ?? 0,
                        MaChucVu = dto.MaChucVu ?? 0,
                        NgayVaoLam = DateOnly.FromDateTime(dto.NgayVaoLam),
                        AvatarUrl = null
                    };

                    _context.NhanViens.Add(nv);
                    _context.SaveChanges();

                    // Tạo tài khoản
                    string username = _usernameGen.GenerateUsername(dto.HoTen, nv.MaNv);
                    string pass = PasswordHelper.CreateTemporaryPassword();
                    string salt = PasswordHelper.GenerateRandomKey();
                    string hash = PasswordHelper.HashTemporaryPassword(pass, salt);

                    var acc = new TaiKhoan
                    {
                        Username = username,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        MaNv = nv.MaNv
                    };

                    _context.TaiKhoans.Add(acc);

                    _context.TaiKhoanQuyenHans.Add(new TaiKhoanQuyenHan
                    {
                        Username = username,
                        MaQuyenHan = "EMPLOYEE"
                    });

                    _context.SaveChanges();
                    transaction.Commit();
                    savedCount++;
                    try
                    {
                        _activation.Send(dto.Email, username, pass, hash);
                    }
                    catch (Exception ex)
                    {
                        activationEmailsFailed++;
                        _logger.LogError(ex, "Không gửi được thư kích hoạt sau khi nhập nhân viên {EmployeeId}.", nv.MaNv);
                    }
                }
                catch (Exception ex)
                {
                    _context.ChangeTracker.Clear();
                    _logger.LogWarning(ex, "Không thể nhập nhân viên tại dòng {RowIndex}.", dto.RowIndex);
                }
            }

            return Json(new { success = true, message = $"Đã lưu {savedCount} nhân viên. Số thư kích hoạt gửi lỗi: {activationEmailsFailed}." });
        }
        #endregion
    }
}
