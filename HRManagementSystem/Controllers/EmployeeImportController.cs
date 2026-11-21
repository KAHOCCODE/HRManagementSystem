using HR_KD.Data;
using HR_KD.DTOs;
using HR_KD.Helpers;
using HR_KD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0 || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ImportError"] = "Chọn tệp Excel .xlsx có dữ liệu.";
                return RedirectToAction(nameof(Index));
            }

            HttpContext.Session.Remove("ImportedEmployees");
            ExcelImportResult result;
            try
            {
                using var stream = file.OpenReadStream();
                result = await _excelService.ParseExcelWithValidation(stream);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException or ArgumentException)
            {
                _logger.LogWarning(ex, "Không đọc được tệp nhập nhân viên.");
                TempData["ImportError"] = "Không đọc được tệp. Hãy kiểm tra lại tệp .xlsx.";
                return RedirectToAction(nameof(Index));
            }

            if (result.Employees.Count == 0)
            {
                TempData["ImportError"] = result.Errors.GetValueOrDefault(0) ?? "Tệp Excel chưa có dữ liệu nhân viên.";
                return RedirectToAction(nameof(Index));
            }

            HttpContext.Session.SetString("ImportedEmployees", JsonConvert.SerializeObject(result.Employees));

            return RedirectToAction("Review");
        }
        #endregion

        #region Xem trước dữ liệu
        public IActionResult Review()
        {
            var json = HttpContext.Session.GetString("ImportedEmployees");

            if (string.IsNullOrEmpty(json)) return RedirectToAction("Index");

            var employees = JsonConvert.DeserializeObject<List<ImportNhanVienDto>>(json);
            ViewBag.ChucVus = _context.ChucVus.ToDictionary(c => c.MaChucVu, c => c.TenChucVu);
            ViewBag.PhongBans = _context.PhongBans.ToDictionary(p => p.MaPhongBan, p => p.TenPhongBan);

            ViewBag.Worksites = _context.Worksites.OrderBy(s => s.Name).ToDictionary(s => s.Id, s => s.Name);
            ViewBag.Managers = _context.NhanViens.OrderBy(n => n.HoTen).ToDictionary(n => n.MaNv, n => n.HoTen);
            return View(employees);
        }
        #endregion

        #region Lưu hàng loạt
        [HttpPost("/api/EmployeesApi/SaveImportBatch")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveImportBatch([FromForm] List<ImportNhanVienDto> list)
        {
            if (list.Count == 0)
            {
                TempData["ImportError"] = "Chưa có dòng nhân viên để lưu.";
                return RedirectToAction(nameof(Index));
            }

            var savedCount = 0;
            var activationEmailsFailed = 0;
            var pending = new List<ImportNhanVienDto>();
            foreach (var dto in list)
            {
                try
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    dto.RowError = await _excelService.ValidateAsync(dto);
                    if (dto.RowError != null)
                    {
                        pending.Add(dto);
                        continue;
                    }

                    var employee = new NhanVien
                    {
                        HoTen = dto.HoTen,
                        NgaySinh = DateOnly.FromDateTime(dto.NgaySinh),
                        GioiTinh = dto.GioiTinh,
                        DiaChi = "",
                        Sdt = dto.Sdt,
                        Email = dto.Email,
                        TrinhDoHocVan = dto.TrinhDoHocVan,
                        MaPhongBan = dto.MaPhongBan.GetValueOrDefault(),
                        MaChucVu = dto.MaChucVu.GetValueOrDefault(),
                        NgayVaoLam = DateOnly.FromDateTime(dto.NgayVaoLam)
                    };

                    _context.NhanViens.Add(employee);
                    await _context.SaveChangesAsync();

                    if (dto.WorksiteId is int siteId)
                        _context.WorksiteAssignments.Add(new WorksiteAssignment
                        {
                            EmployeeId = employee.MaNv,
                            WorksiteId = siteId,
                            EffectiveFrom = employee.NgayVaoLam.Value
                        });
                    if (dto.ManagerId is int managerId)
                        _context.EmployeeReportingLines.Add(new EmployeeReportingLine
                        {
                            EmployeeId = employee.MaNv,
                            ManagerId = managerId
                        });

                    string username = _usernameGen.GenerateUsername(dto.HoTen, employee.MaNv);
                    string pass = PasswordHelper.CreateTemporaryPassword();
                    string salt = PasswordHelper.GenerateRandomKey();
                    string hash = PasswordHelper.HashTemporaryPassword(pass, salt);
                    _context.TaiKhoans.Add(new TaiKhoan
                    {
                        Username = username,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        MaNv = employee.MaNv
                    });
                    _context.TaiKhoanQuyenHans.Add(new TaiKhoanQuyenHan
                    {
                        Username = username,
                        MaQuyenHan = "EMPLOYEE"
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    savedCount++;
                    try
                    {
                        _activation.Send(dto.Email, username, pass, hash);
                    }
                    catch (Exception ex)
                    {
                        activationEmailsFailed++;
                        _logger.LogError(ex, "Không gửi được thư kích hoạt cho nhân viên {EmployeeId}.", employee.MaNv);
                    }
                }
                catch (Exception ex)
                {
                    _context.ChangeTracker.Clear();
                    _logger.LogWarning(ex, "Không thể nhập nhân viên tại dòng {RowIndex}.", dto.RowIndex);
                    dto.RowError = "Không lưu được dòng này. Kiểm tra dữ liệu rồi thử lại.";
                    pending.Add(dto);
                }
            }

            var summary = $"Đã lưu {savedCount} nhân viên. Còn {pending.Count} dòng cần kiểm tra.";
            if (activationEmailsFailed > 0)
                summary += $" Có {activationEmailsFailed} thư kích hoạt chưa gửi được.";
            TempData["ImportSummary"] = summary;
            if (pending.Count > 0)
            {
                HttpContext.Session.SetString("ImportedEmployees", JsonConvert.SerializeObject(pending));
                return RedirectToAction(nameof(Review));
            }

            HttpContext.Session.Remove("ImportedEmployees");
            return RedirectToAction(nameof(Index));
        }
        #endregion
    }
}
