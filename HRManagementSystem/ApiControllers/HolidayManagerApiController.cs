using HR_KD.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;

namespace HR_KD.Controllers
{
    [ApiController]
    [Route("api/holidaymanager")]
    [Authorize(Roles = "DIRECTOR")]
    public class HolidayManagerApiController : ControllerBase
    {
        private readonly HrDbContext _context;
        private readonly IConfiguration _configuration;

        public HolidayManagerApiController(HrDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NgayLe>>> GetHolidays(int? year)
        {
            try
            {
                IQueryable<NgayLe> holidays = _context.NgayLes;
                if (year.HasValue)
                {
                    holidays = holidays.Where(h => h.NgayLe1.Year == year.Value);
                }
                var result = await holidays.ToListAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi tải danh sách ngày lễ.", error = ex.Message });
            }
        }

        [HttpGet("years")]
        public async Task<ActionResult<IEnumerable<int>>> GetHolidayYears()
        {
            try
            {
                var years = await _context.NgayLes
                    .Select(h => h.NgayLe1.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync();
                return Ok(years);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi tải danh sách năm.", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NgayLe>> GetHoliday(int id)
        {
            try
            {
                var holiday = await _context.NgayLes.FindAsync(id);
                if (holiday == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy ngày lễ." });
                }
                return Ok(holiday);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi tải thông tin ngày lễ.", error = ex.Message });
            }
        }

        [HttpPost("approve/{id}")]
        public async Task<IActionResult> Approve(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var holiday = await _context.NgayLes.FindAsync(id);
            if (holiday == null)
                return NotFound(new { success = false, message = "Không tìm thấy ngày lễ." });
            if (holiday.TrangThai is TrangThai.NL4 or TrangThai.NL5)
                return Ok(new { success = true, message = "Ngày lễ đã được duyệt." });
            if (holiday.TrangThai is not (TrangThai.NL1 or TrangThai.NL2 or TrangThai.NL3))
                return Conflict(new { success = false, message = "Đưa ngày lễ về trạng thái chờ duyệt trước khi duyệt lại." });
            if (await HasPayroll(holiday.NgayLe1))
                return PayrollConflict();

            await ApplyApproval(holiday);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { success = true, message = "Đã duyệt ngày lễ." });
        }

        [HttpPost("reject/{id}")]
        
        public async Task<IActionResult> Reject(int id)
        {
            try
            {
                var rejecterMaNV = User.Claims.FirstOrDefault(c => c.Type == "MaNV")?.Value;
                var rejecterName = User.Claims.FirstOrDefault(c => c.Type == "TenNV")?.Value;

                if (string.IsNullOrEmpty(rejecterMaNV))
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy thông tin người từ chối." });
                }

                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var holiday = await _context.NgayLes.FindAsync(id);
                if (holiday == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy ngày lễ." });
                }

                if (holiday.TrangThai is TrangThai.NL4 or TrangThai.NL5)
                    return Conflict(new { success = false, message = "Hủy duyệt trước khi từ chối ngày lễ." });
                if (holiday.TrangThai == TrangThai.NL6)
                    return Ok(new { success = true, message = "Ngày lễ đã bị từ chối." });

                holiday.TrangThai = TrangThai.NL6;
                holiday.MoTa = $"{holiday.MoTa ?? ""}\nBị từ chối bởi: {rejecterName} ({rejecterMaNV}) vào ngày {DateTime.Now:dd/MM/yyyy HH:mm}";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = $"Ngày lễ ID {id} đã bị từ chối bởi {rejecterName}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi từ chối ngày lễ.", error = ex.Message });
            }
        }

        [HttpPost("cancel/{id}")]
        public async Task<IActionResult> Cancel(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var holiday = await _context.NgayLes.FindAsync(id);
            if (holiday == null)
                return NotFound(new { success = false, message = "Không tìm thấy ngày lễ." });
            if (holiday.TrangThai is TrangThai.NL1 or TrangThai.NL2 or TrangThai.NL3)
                return Ok(new { success = true, message = "Ngày lễ đang chờ duyệt." });

            if (holiday.TrangThai is TrangThai.NL4 or TrangThai.NL5)
            {
                if (await HasPayroll(holiday.NgayLe1))
                    return PayrollConflict();

                var holidayNote = $"Ngày lễ: {holiday.TenNgayLe} - Được duyệt bởi: ";
                var makeupNote = $"Ngày nghỉ bù: {holiday.TenNgayLe} - Được duyệt bởi: ";
                // Both prefixes cover records generated by earlier single and bulk approvals.
                var generated = await _context.ChamCongs
                    .Where(x => x.NgayLamViec == holiday.NgayLe1 && x.TrangThai == "CC3"
                        && x.MaNvDuyet == 0 && x.NgayDuyet == null && x.GhiChu != null
                        && (x.GhiChu.StartsWith(holidayNote) || x.GhiChu.StartsWith(makeupNote)))
                    .ToListAsync();
                _context.ChamCongs.RemoveRange(generated);
            }

            holiday.TrangThai = holiday.TenNgayLe.Contains("Nghỉ bù") ? TrangThai.NL3 :
                holiday.NgayLe1.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? TrangThai.NL2 : TrangThai.NL1;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { success = true, message = "Đã đưa ngày lễ về trạng thái chờ duyệt." });
        }

        [HttpPost("approve/year/{year}")]
        public async Task<IActionResult> ApproveAllByYear(int year)
        {
            if (year is < 1 or > 9999)
                return BadRequest(new { success = false, message = "Năm không hợp lệ." });

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var pending = await _context.NgayLes
                .Where(x => x.NgayLe1.Year == year &&
                    (x.TrangThai == TrangThai.NL1 || x.TrangThai == TrangThai.NL2 || x.TrangThai == TrangThai.NL3))
                .ToListAsync();
            var lockedMonths = await _context.BangLuongs.Where(x => x.ThangNam.Year == year)
                .Select(x => x.ThangNam.Month).Distinct().ToListAsync();
            if (pending.Any(x => lockedMonths.Contains(x.NgayLe1.Month)))
                return PayrollConflict();

            foreach (var holiday in pending)
                await ApplyApproval(holiday);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { success = true, message = $"Đã duyệt {pending.Count} ngày lễ trong năm {year}." });
        }

        private Task<bool> HasPayroll(DateOnly day) =>
            _context.BangLuongs.AnyAsync(x => x.ThangNam.Year == day.Year && x.ThangNam.Month == day.Month);

        private ConflictObjectResult PayrollConflict() =>
            Conflict(new { success = false, message = "Kỳ này đã có bảng lương. Đối soát bảng lương trước khi đổi ngày lễ." });

        private async Task ApplyApproval(NgayLe holiday)
        {
            var approverName = User.FindFirst("TenNV")?.Value ?? User.Identity?.Name;
            var approverId = User.FindFirst("MaNV")?.Value;
            if (holiday.TrangThai is TrangThai.NL1 or TrangThai.NL3)
            {
                var label = holiday.TrangThai == TrangThai.NL3 ? "Ngày nghỉ bù" : "Ngày lễ";
                var employees = await _context.NhanViens.Where(x =>
                    (x.NgayVaoLam == null || x.NgayVaoLam <= holiday.NgayLe1)
                    && !_context.ChamCongs.Any(c => c.MaNv == x.MaNv && c.NgayLamViec == holiday.NgayLe1))
                    .Select(x => x.MaNv).ToListAsync();
                _context.ChamCongs.AddRange(employees.Select(employeeId => new ChamCong
                {
                    MaNv = employeeId,
                    NgayLamViec = holiday.NgayLe1,
                    GioVao = new TimeOnly(8, 0),
                    GioRa = new TimeOnly(18, 0),
                    TongGio = 8.0m,
                    TrangThai = "CC3",
                    GhiChu = $"{label}: {holiday.TenNgayLe} - Được duyệt bởi: {approverName}"
                }));
                holiday.TrangThai = TrangThai.NL4;
            }
            else
            {
                holiday.TrangThai = TrangThai.NL5;
            }
            holiday.MoTa = $"{holiday.MoTa ?? ""}\nĐược duyệt bởi: {approverName} ({approverId}) vào ngày {DateTime.Now:dd/MM/yyyy HH:mm}";
        }

        [HttpGet("requests")]
       
        public async Task<ActionResult<IEnumerable<YeuCau>>> GetRequests()
        {
            try
            {
                var requests = await _context.YeuCaus
                    .Where(y => y.TenYeuCau == "Yêu cầu mở duyệt ngày lễ")
                    .OrderByDescending(y => y.NgayTao)
                    .ToListAsync();
                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi tải danh sách yêu cầu.", error = ex.Message });
            }
        }

        [HttpPost("requests/approve/{id}")]
        
        public async Task<IActionResult> ApproveRequest(int id)
        {
            try
            {
                var approverMaNV = User.Claims.FirstOrDefault(c => c.Type == "MaNV")?.Value;
                var approverName = User.Claims.FirstOrDefault(c => c.Type == "TenNV")?.Value;

                if (string.IsNullOrEmpty(approverMaNV))
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy thông tin người duyệt." });
                }

                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var request = await _context.YeuCaus.FindAsync(id);
                if (request == null || request.TenYeuCau != "Yêu cầu mở duyệt ngày lễ")
                {
                    return NotFound(new { success = false, message = "Không tìm thấy yêu cầu." });
                }

                if (request.TrangThai || request.NgayDuyet.HasValue)
                {
                    return BadRequest(new { success = false, message = "Yêu cầu đã được xử lý trước đó." });
                }

                request.TrangThai = true;
                request.MaNvDuyet = approverMaNV;
                request.NgayDuyet = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                

                return Ok(new { success = true, message = $"Yêu cầu ID {id} đã được duyệt bởi {approverName}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi duyệt yêu cầu.", error = ex.Message });
            }
        }

        [HttpPost("requests/reject/{id}")]
       
        public async Task<IActionResult> RejectRequest(int id)
        {
            try
            {
                var rejecterMaNV = User.Claims.FirstOrDefault(c => c.Type == "MaNV")?.Value;
                var rejecterName = User.Claims.FirstOrDefault(c => c.Type == "TenNV")?.Value;

                if (string.IsNullOrEmpty(rejecterMaNV))
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy thông tin người từ chối." });
                }

                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var request = await _context.YeuCaus.FindAsync(id);
                if (request == null || request.TenYeuCau != "Yêu cầu mở duyệt ngày lễ")
                {
                    return NotFound(new { success = false, message = "Không tìm thấy yêu cầu." });
                }

                if (request.TrangThai || request.NgayDuyet.HasValue)
                {
                    return BadRequest(new { success = false, message = "Yêu cầu đã được xử lý trước đó." });
                }

                request.TrangThai = false;
                request.MaNvDuyet = rejecterMaNV;
                request.NgayDuyet = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                

                return Ok(new { success = true, message = $"Yêu cầu ID {id} đã bị từ chối bởi {rejecterName}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi từ chối yêu cầu.", error = ex.Message });
            }
        }

        [HttpPost("requests/cancel/{id}")]
        
        public async Task<IActionResult> CancelRequest(int id)
        {
            try
            {
                var cancellerMaNV = User.Claims.FirstOrDefault(c => c.Type == "MaNV")?.Value;
                var cancellerName = User.Claims.FirstOrDefault(c => c.Type == "TenNV")?.Value;

                if (string.IsNullOrEmpty(cancellerMaNV))
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy thông tin người hủy." });
                }

                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var request = await _context.YeuCaus.FindAsync(id);
                if (request == null || request.TenYeuCau != "Yêu cầu mở duyệt ngày lễ")
                {
                    return NotFound(new { success = false, message = "Không tìm thấy yêu cầu." });
                }

                if (!request.TrangThai && !request.NgayDuyet.HasValue)
                {
                    return BadRequest(new { success = false, message = "Yêu cầu chưa được xử lý, không thể hủy." });
                }

                request.TrangThai = false;
                request.MaNvDuyet = null;
                request.NgayDuyet = null;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                

                return Ok(new { success = true, message = $"Yêu cầu ID {id} đã được hủy bởi {cancellerName} và trạng thái trở về 'Chờ xử lý'." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi hủy yêu cầu.", error = ex.Message });
            }
        }

        
    }
}