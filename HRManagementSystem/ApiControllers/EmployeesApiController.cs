using HR_KD.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HR_KD.DTOs;
using System.IO;
using System.Collections.Generic;
using HR_KD.Helpers;
using HR_KD.Services;
using Microsoft.AspNetCore.Authorization;

namespace HR_KD.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "DIRECTOR,EMPLOYEE_MANAGER")]
    public class EmployeesApiController : ControllerBase
    {
        private readonly HrDbContext _context;
        private readonly AccountActivationService _activation;
        private readonly UsernameGeneratorService _usernameGen;
        private readonly ILogger<EmployeesApiController> _logger;

        public EmployeesApiController(HrDbContext context, AccountActivationService activation, ILogger<EmployeesApiController> logger)
        {
            _context = context;
            _activation = activation;
            _logger = logger;
            _usernameGen = new UsernameGeneratorService();
        }
        #region Kiểm tra vai trò người dùng
        [HttpGet("CheckRole")]
        public IActionResult CheckRole()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { message = "Người dùng chưa đăng nhập." });
                }

                var currentEmployee = _context.TaiKhoans
                    .Include(t => t.MaNvNavigation)
                    .FirstOrDefault(t => t.Username == username);

                if (currentEmployee == null || currentEmployee.MaNvNavigation == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin nhân viên của người dùng." });
                }

                bool isDirector = _context.TaiKhoanQuyenHans
                    .Any(tq => tq.Username == username && tq.MaQuyenHan == "DIRECTOR");

                return Ok(new
                {
                    isDirector,
                    maPhongBan = currentEmployee.MaNvNavigation.MaPhongBan
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra vai trò người dùng.");
                return StatusCode(500, new { message = "Không xử lý được yêu cầu." });
            }
        }
        #endregion

        #region Lấy danh sách nhân viên
        [HttpGet]
        public IActionResult GetEmployees([FromQuery] int? phongBan = null, [FromQuery] int? chucVu = null, [FromQuery] string? search = null, [FromQuery] int? worksiteId = null)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
                var query = _context.NhanViens.AsNoTracking();
                if (phongBan.HasValue)
                    query = query.Where(e => e.MaPhongBan == phongBan.Value);
                if (worksiteId.HasValue)
                    query = query.Where(e => _context.WorksiteAssignments
                        .Where(site => site.EmployeeId == e.MaNv && site.EffectiveFrom <= today)
                        .OrderByDescending(site => site.EffectiveFrom).Select(site => (int?)site.WorksiteId)
                        .FirstOrDefault() == worksiteId);

                // Áp dụng bộ lọc chức vụ nếu có
                if (chucVu.HasValue)
                {
                    query = query.Where(e => e.MaChucVu == chucVu.Value);
                }

                // Áp dụng tìm kiếm nếu có
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(e =>
                        e.HoTen.ToLower().Contains(search) ||
                        (e.Email != null && e.Email.ToLower().Contains(search)) ||
                        (e.Sdt != null && e.Sdt.Contains(search)));
                }

                var employees = query.OrderBy(e => e.MaNv).Select(e => new
                {
                    e.MaNv,
                    e.HoTen,
                    e.NgaySinh,
                    GioiTinh = e.GioiTinh.HasValue ? (e.GioiTinh.Value ? "Nam" : "Nữ") : "Không xác định",
                    e.DiaChi,
                    e.Sdt,
                    e.Email,
                    e.TrinhDoHocVan,
                    e.NgayVaoLam,
                    ChucVu = e.MaChucVuNavigation.TenChucVu,
                    PhongBan = e.MaPhongBanNavigation.TenPhongBan,
                    e.AvatarUrl,
                    e.SoNguoiPhuThuoc,
                    e.MaPhongBan,
                    e.MaChucVu,
                    Worksite = _context.WorksiteAssignments.Where(site => site.EmployeeId == e.MaNv && site.EffectiveFrom <= today)
                        .OrderByDescending(site => site.EffectiveFrom).Select(site => site.Worksite.Name).FirstOrDefault()
                })
                .ToList();

                return Ok(employees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách nhân viên.");
                return StatusCode(500, new { message = "Không xử lý được yêu cầu." });
            }
        }
        #endregion

        #region Lấy danh sách hợp đồng nhân viên
        [HttpGet("GetEmployeeContracts")]
        public IActionResult GetEmployeeContracts()
        {
            var contracts = _context.NhanViens
                .GroupJoin(_context.HopDongLaoDongs,
                    nv => nv.MaNv,
                    hd => hd.MaNv,
                    (nv, hd) => new { NhanVien = nv, HopDong = hd })
                .SelectMany(
                    x => x.HopDong.DefaultIfEmpty(),
                    (nv, hd) => new
                    {
                        MaNv = nv.NhanVien.MaNv,
                        HoTen = nv.NhanVien.HoTen,
                        MaHopDong = hd != null ? hd.MaHopDong : (int?)null,
                        MaLoaiHopDong = hd != null ? hd.MaLoaiHopDong : (int?)null,
                        TenLoaiHopDong = hd != null && hd.LoaiHopDong != null ? hd.LoaiHopDong.TenLoaiHopDong : null,
                        ThoiHan = hd != null ? hd.ThoiHan : null,
                        NgayBatDau = hd != null ? hd.NgayBatDau : null,
                        NgayKetThuc = hd != null ? hd.NgayKetThuc : null,
                        GhiChu = hd != null ? hd.GhiChu : null,
                        SoLanGiaHan = hd != null ? hd.SoLanGiaHan : null,
                        GiaHanToiDa = hd != null && hd.LoaiHopDong != null ? hd.LoaiHopDong.GiaHanToiDa : null
                    })
                .ToList();
            return Ok(contracts);
        }
        #endregion

        #region Lấy hợp đồng của một nhân viên
        [HttpGet("GetEmployeeContract/{maNv}")]
        public IActionResult GetEmployeeContract(int maNv)
        {
            var contract = _context.HopDongLaoDongs
                .Where(hd => hd.MaNv == maNv && hd.IsActive)
                .Select(hd => new
                {
                    hd.MaHopDong,
                    hd.MaLoaiHopDong,
                    hd.ThoiHan,
                    hd.NgayBatDau,
                    hd.NgayKetThuc,
                    hd.GhiChu,
                    hd.SoLanGiaHan
                })
                .FirstOrDefault();

            if (contract == null)
            {
                return Ok(new { });
            }

            return Ok(contract);
        }
        #endregion

        #region Kiểm tra nhân viên đã có thông tin lương hay chưa
        [HttpGet("CheckEmployeeSalary/{maNv}")]
        public IActionResult CheckEmployeeSalary(int maNv)
        {
            try
            {
                if (!_context.NhanViens.Any(nv => nv.MaNv == maNv))
                {
                    return BadRequest(new { message = "Nhân viên không tồn tại." });
                }

                bool hasSalary = _context.ThongTinLuongNVs.Any(l => l.MaNv == maNv);

                return Ok(new { hasSalary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra thông tin lương của nhân viên.");
                return StatusCode(500, new { message = "Không xử lý được yêu cầu." });
            }
        }
        #endregion

        #region Thêm hoặc cập nhật hợp đồng
        [HttpPost("SaveContract")]
        public IActionResult SaveContract([FromBody] ContractDTO contractDto)
        {
            if (contractDto == null)
            {
                return BadRequest(new { message = "Dữ liệu hợp đồng không được để trống." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { message = string.Join(" | ", errors) });
            }

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                if (contractDto.MaNv <= 0)
                {
                    return BadRequest(new { message = "Mã nhân viên không hợp lệ." });
                }

                if (!_context.NhanViens.Any(nv => nv.MaNv == contractDto.MaNv))
                {
                    return BadRequest(new { message = "Nhân viên không tồn tại." });
                }

                var loaiHopDong = _context.LoaiHopDongs
                    .FirstOrDefault(l => l.MaLoaiHopDong == contractDto.MaLoaiHopDong);
                if (loaiHopDong == null)
                {
                    return BadRequest(new { message = "Loại hợp đồng không tồn tại." });
                }

                if (contractDto.NgayBatDau == default)
                {
                    return BadRequest(new { message = "Ngày bắt đầu là bắt buộc." });
                }

                if (contractDto.MaLoaiHopDong != 2)
                {
                    if (loaiHopDong.ThoiHanMacDinh.HasValue && contractDto.ThoiHan.HasValue)
                    {
                        if (contractDto.ThoiHan.Value > loaiHopDong.ThoiHanMacDinh.Value)
                        {
                            return BadRequest(new { message = $"Thời hạn hợp đồng không được vượt quá {loaiHopDong.ThoiHanMacDinh.Value} tháng." });
                        }
                    }
                    else if (!loaiHopDong.ThoiHanMacDinh.HasValue && contractDto.ThoiHan.HasValue)
                    {
                        return BadRequest(new { message = "Loại hợp đồng này không cho phép nhập thời hạn." });
                    }
                }
                else
                {
                    contractDto.ThoiHan = null;
                    contractDto.NgayKetThuc = null;
                }

                DateOnly? ngayKetThuc = null;
                if (contractDto.MaLoaiHopDong != 2)
                {
                    if (!string.IsNullOrEmpty(contractDto.NgayKetThuc?.ToString()))
                    {
                        ngayKetThuc = DateOnly.FromDateTime(contractDto.NgayKetThuc.Value);
                    }
                    else if (contractDto.ThoiHan.HasValue && contractDto.ThoiHan > 0)
                    {
                        ngayKetThuc = DateOnly.FromDateTime(contractDto.NgayBatDau.AddMonths(contractDto.ThoiHan.Value));
                    }
                }

                if (contractDto.MaHopDong > 0)
                {
                    var existingContract = _context.HopDongLaoDongs
                        .FirstOrDefault(hd => hd.MaHopDong == contractDto.MaHopDong && hd.MaNv == contractDto.MaNv);

                    if (existingContract == null)
                    {
                        return NotFound(new { message = "Hợp đồng không tồn tại." });
                    }

                    existingContract.MaLoaiHopDong = contractDto.MaLoaiHopDong;
                    existingContract.ThoiHan = contractDto.ThoiHan;
                    existingContract.NgayBatDau = DateOnly.FromDateTime(contractDto.NgayBatDau);
                    existingContract.NgayKetThuc = ngayKetThuc;
                    existingContract.GhiChu = contractDto.GhiChu;
                    existingContract.IsActive = true;

                    _context.HopDongLaoDongs.Update(existingContract);
                }
                else
                {
                    var newContract = new HopDongLaoDong
                    {
                        MaNv = contractDto.MaNv,
                        MaLoaiHopDong = contractDto.MaLoaiHopDong,
                        ThoiHan = contractDto.ThoiHan,
                        NgayBatDau = DateOnly.FromDateTime(contractDto.NgayBatDau),
                        NgayKetThuc = ngayKetThuc,
                        GhiChu = contractDto.GhiChu,
                        SoLanGiaHan = 0,
                        IsActive = true
                    };

                    _context.HopDongLaoDongs.Add(newContract);
                }

                _context.SaveChanges();
                transaction.Commit();

                return Ok(new { message = "Hợp đồng đã được lưu thành công.", maNv = contractDto.MaNv });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Lỗi khi lưu hợp đồng.");
                return StatusCode(500, new { message = "Không xử lý được yêu cầu." });
            }
        }
        #endregion

        #region Gia hạn hợp đồng
        [HttpPost("ExtendContract")]
        public IActionResult ExtendContract([FromBody] ExtendContractDTO extendDto)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var contract = _context.HopDongLaoDongs
                    .Include(hd => hd.LoaiHopDong)
                    .FirstOrDefault(hd => hd.MaHopDong == int.Parse(extendDto.MaHopDong) && hd.MaNv == int.Parse(extendDto.MaNv));

                if (contract == null)
                {
                    return NotFound(new { message = "Hợp đồng không tồn tại." });
                }

                if (!contract.IsActive)
                {
                    return BadRequest(new { message = "Hợp đồng không còn hiệu lực." });
                }

                if (!contract.NgayKetThuc.HasValue)
                {
                    return BadRequest(new { message = "Hợp đồng không có ngày kết thúc để gia hạn." });
                }

                var currentDate = DateOnly.FromDateTime(DateTime.Today);
                if (contract.NgayKetThuc < currentDate)
                {
                    return BadRequest(new { message = "Hợp đồng đã hết hạn, không thể gia hạn." });
                }

                var soLanGiaHan = contract.SoLanGiaHan ?? 0;
                var giaHanToiDa = contract.LoaiHopDong.GiaHanToiDa;

                if (extendDto.ConvertToUnlimited)
                {
                    var loaiHopDongKhongThoiHan = _context.LoaiHopDongs
                        .FirstOrDefault(l => l.MaLoaiHopDong == 2);
                    if (loaiHopDongKhongThoiHan == null)
                    {
                        return BadRequest(new { message = "Loại hợp đồng không xác định thời hạn không tồn tại." });
                    }

                    contract.MaLoaiHopDong = 2;
                    contract.ThoiHan = null;
                    contract.NgayKetThuc = null;
                    contract.SoLanGiaHan = soLanGiaHan;
                    contract.GhiChu = (contract.GhiChu ?? "") + $"\nChuyển sang hợp đồng không xác định thời hạn vào {currentDate}.";
                }
                else
                {
                    if (giaHanToiDa.HasValue && soLanGiaHan >= giaHanToiDa.Value)
                    {
                        return BadRequest(new { message = "Hợp đồng đã đạt số lần gia hạn tối đa." });
                    }

                    contract.ThoiHan = 12;
                    contract.NgayKetThuc = contract.NgayKetThuc.Value.AddMonths(12);
                    contract.SoLanGiaHan = soLanGiaHan + 1;
                    contract.GhiChu = (contract.GhiChu ?? "") + $"\nGia hạn hợp đồng lần {contract.SoLanGiaHan} vào {currentDate}.";
                }

                _context.HopDongLaoDongs.Update(contract);
                _context.SaveChanges();
                transaction.Commit();

                return Ok(new { message = extendDto.ConvertToUnlimited ? "Chuyển sang hợp đồng không xác định thời hạn thành công." : "Gia hạn hợp đồng thành công." });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Lỗi khi gia hạn hợp đồng.");
                return StatusCode(500, new { message = "Không xử lý được yêu cầu." });
            }
        }
        #endregion

        #region Thêm nhân viên 
        [HttpPost("CreateEmployee")]
        public IActionResult CreateEmployee([FromForm] CreateEmployeeDTO employeeDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { message = string.Join(" | ", errors) });
            }

            if (employeeDto.AvatarUrl is { } avatar && !EmployeePhotos.IsValid(avatar))
                return BadRequest(new { message = "Ảnh đại diện phải là JPG, PNG hoặc WebP và tối đa 5 MB." });

            if (employeeDto.NgaySinh == default || employeeDto.NgayVaoLam == default
                || employeeDto.NgaySinh.Date >= employeeDto.NgayVaoLam.Date)
                return BadRequest(new { message = "Ngày sinh và ngày vào làm không hợp lệ." });

            using var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            string? uploaded = null;
            try
            {
                var email = employeeDto.Email.Trim();
                var phone = employeeDto.Sdt.Trim();
                if (_context.NhanViens.Any(x => x.Email != null && x.Email.ToLower() == email.ToLower()))
                    return Conflict(new { message = "Email đã được sử dụng." });
                if (_context.NhanViens.Any(x => x.Sdt == phone))
                    return Conflict(new { message = "Số điện thoại đã được sử dụng." });
                if (employeeDto.WorksiteId is int site && !_context.Worksites.Any(x => x.Id == site))
                    return BadRequest(new { message = "Nơi làm việc không tồn tại." });
                if (employeeDto.ManagerId is int manager && !_context.NhanViens.Any(x => x.MaNv == manager))
                    return BadRequest(new { message = "Quản lý trực tiếp không tồn tại." });

                if (!_context.PhongBans.Any(p => p.MaPhongBan == employeeDto.MaPhongBan))
                {
                    return BadRequest(new { message = "Phòng ban không tồn tại." });
                }

                if (!_context.ChucVus.Any(c => c.MaChucVu == employeeDto.MaChucVu))
                {
                    return BadRequest(new { message = "Chức vụ không tồn tại." });
                }

                var employee = new NhanVien
                {
                    HoTen = employeeDto.HoTen.Trim(),
                    NgaySinh = DateOnly.FromDateTime(employeeDto.NgaySinh),
                    GioiTinh = employeeDto.GioiTinh,
                    DiaChi = employeeDto.DiaChi,
                    Sdt = phone,
                    Email = email,
                    TrinhDoHocVan = employeeDto.TrinhDoHocVan,
                    MaPhongBan = employeeDto.MaPhongBan,
                    MaChucVu = employeeDto.MaChucVu,
                    NgayVaoLam = DateOnly.FromDateTime(employeeDto.NgayVaoLam),
                    AvatarUrl = null,
                    SoNguoiPhuThuoc = employeeDto.SoNguoiPhuThuoc
                };

                _context.NhanViens.Add(employee);
                _context.SaveChanges();
                int maNvMoi = employee.MaNv;
                if (employeeDto.WorksiteId is int worksiteId)
                    _context.WorksiteAssignments.Add(new WorksiteAssignment
                    {
                        EmployeeId = maNvMoi, WorksiteId = worksiteId, EffectiveFrom = employee.NgayVaoLam!.Value
                    });
                if (employeeDto.ManagerId is int managerId)
                    _context.EmployeeReportingLines.Add(new EmployeeReportingLine { EmployeeId = maNvMoi, ManagerId = managerId });

                var defaultRoles = new List<string> { "EMPLOYEE" };
                var validRoles = _context.QuyenHans
                    .Where(q => defaultRoles.Contains(q.MaQuyenHan))
                    .Select(q => q.MaQuyenHan)
                    .ToList();

                if (!validRoles.Any())
                {
                    _logger.LogWarning("Không có quyền hạn hợp lệ để gán cho nhân viên mới.");
                    return BadRequest(new { message = "Không có quyền hạn hợp lệ để gán cho nhân viên mới." });
                }

                string username = _usernameGen.GenerateUsername(string.IsNullOrEmpty(employeeDto.HoTen) ? "user" : employeeDto.HoTen, maNvMoi);
                string temporaryPassword = PasswordHelper.CreateTemporaryPassword();
                string randomkey = PasswordHelper.GenerateRandomKey();
                string hashedPassword = PasswordHelper.HashTemporaryPassword(temporaryPassword, randomkey);

                var taiKhoan = new TaiKhoan
                {
                    Username = username,
                    PasswordHash = hashedPassword,
                    PasswordSalt = randomkey,
                    MaNv = maNvMoi
                };
                _context.TaiKhoans.Add(taiKhoan);
                _context.SaveChanges();

                foreach (var role in validRoles)
                {
                    _context.TaiKhoanQuyenHans.Add(new TaiKhoanQuyenHan
                    {
                        Username = taiKhoan.Username,
                        MaQuyenHan = role
                    });
                }
                _context.SaveChanges();

                var startDate = employee.NgayVaoLam;
                if (!startDate.HasValue)
                {
                    _logger.LogError("NgayVaoLam is null for employee {MaNv}", maNvMoi);
                    return BadRequest(new { message = "Ngày vào làm không được để trống." });
                }
                var year = startDate.Value.Year;
                var holidays = _context.NgayLes
                    .Where(nl => nl.TrangThai == "NL4" && nl.NgayLe1.Year == year)
                    .ToList();

                foreach (var holiday in holidays)
                {
                    int days = holiday.SoNgayNghi.GetValueOrDefault(1);
                    for (int i = 0; i < days; i++)
                    {
                        var holidayDate = holiday.NgayLe1.AddDays(i);
                        if (holidayDate >= startDate.Value && holidayDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) && !_context.ChamCongs.Any(cc => cc.MaNv == maNvMoi && cc.NgayLamViec == holidayDate))
                        {
                            var attendance = new ChamCong
                            {
                                MaNv = maNvMoi,
                                NgayLamViec = holidayDate,
                                TongGio = 8,
                                TrangThai = HR_KD.Common.AttendanceStatus.Paidleave,
                                GhiChu = holiday.TenNgayLe,
                                MaNvDuyet = int.Parse(User.FindFirst("MaNV")!.Value)
                            };

                            _context.ChamCongs.Add(attendance);
                            _logger.LogInformation("Created attendance record for MaNv {MaNv} on holiday {NgayLamViec} ({TenNgayLe})", maNvMoi, holidayDate, holiday.TenNgayLe);
                        }
                    }
                }
                _context.SaveChanges();

                if (employeeDto.AvatarUrl != null)
                {
                    employee.AvatarUrl = uploaded = EmployeePhotos.Save(employeeDto.AvatarUrl, maNvMoi);
                    _context.SaveChanges();
                }

                transaction.Commit();

                var activationSent = false;
                try
                {
                    _activation.Send(email, taiKhoan.Username, temporaryPassword, taiKhoan.PasswordHash);
                    activationSent = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi gửi email.");
                }

                var responseDto = new
                {
                    MaNv = employee.MaNv,
                    ActivationSent = activationSent,
                    HoTen = employee.HoTen,
                    NgaySinh = employee.NgaySinh,
                    GioiTinh = employee.GioiTinh.HasValue ? (employee.GioiTinh.Value ? "Nam" : "Nữ") : "Không xác định",
                    DiaChi = employee.DiaChi,
                    Sdt = employee.Sdt,
                    Email = employee.Email,
                    TrinhDoHocVan = employee.TrinhDoHocVan,
                    NgayVaoLam = employee.NgayVaoLam,
                    MaPhongBan = employee.MaPhongBan,
                    MaChucVu = employee.MaChucVu,
                    AvatarUrl = employee.AvatarUrl,
                    SoNguoiPhuThuoc = employee.SoNguoiPhuThuoc
                };

                return CreatedAtAction(nameof(GetEmployees), new { id = maNvMoi }, responseDto);
            }
            catch (Exception ex)
            {
                EmployeePhotos.DeleteNewUpload(uploaded);
                _logger.LogError(ex, "Lỗi khi tạo nhân viên.");
                return StatusCode(500, new { message = "Không xử lý được yêu cầu." });
            }
        }
        #endregion
    }
}