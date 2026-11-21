using HR_KD.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HR_KD.Controllers
{
    [ApiController]
    [Route("api/Holidays")]
    [Authorize]
    public class HolidayApiController : ControllerBase
    {
        private readonly HrDbContext _context;

        public HolidayApiController(HrDbContext context)
        {
            _context = context;
        }
        [HttpGet("GetFixedHolidays")]
        public async Task<ActionResult<IEnumerable<NgayLeCoDinh>>> GetFixedHolidays()
        {
            var fixedHolidays = await _context.NgayLeCoDinhs.ToListAsync();
            return Ok(fixedHolidays);
        }

        [HttpGet("GetAll")]
        public async Task<ActionResult> GetAll()
        {
            try
            {
                var holidays = await _context.NgayLes.ToListAsync();
                return Ok(holidays);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi tải danh sách ngày lễ.", error = ex.Message });
            }
        }

        [HttpPost("Add")]
        [Authorize(Roles = "EMPLOYEE_MANAGER")]
        public async Task<ActionResult> Add([FromBody] NgayLe holiday)
        {
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(holiday.TenNgayLe) || holiday.NgayLe1 == default)
                {
                    return BadRequest(new { success = false, message = "Tên ngày lễ và ngày lễ là bắt buộc." });
                }

                // Check if holiday management is allowed
                var currentDate = DateTime.Now;
                var currentYear = currentDate.Year;
                var isInPeriod = currentDate.Month == 12 && currentDate.Day >= 1 && currentDate.Day <= 30;
                var hasApprovedRequest = await _context.YeuCaus
                    .AnyAsync(y => y.TenYeuCau == "Yêu cầu mở duyệt ngày lễ" &&
                                   y.MoTa.Contains($"năm {currentYear}") &&
                                   y.TrangThai &&
                                   y.NgayDuyet.HasValue &&
                                   y.NgayDuyet.Value >= DateTime.Now.AddDays(-30));

                if (!isInPeriod && !hasApprovedRequest)
                {
                    return BadRequest(new { success = false, message = "Chức năng thêm ngày lễ chỉ được mở trong khoảng thời gian từ 1/12 đến 30/12 hoặc khi có yêu cầu được duyệt cho năm hiện tại." });
                }

                // Check for duplicate holiday
                var existingHoliday = await _context.NgayLes
                    .FirstOrDefaultAsync(h => h.NgayLe1 == holiday.NgayLe1);
                if (existingHoliday != null)
                {
                    return BadRequest(new { success = false, message = $"Ngày lễ '{existingHoliday.TenNgayLe}' đã tồn tại cho ngày {holiday.NgayLe1}." });
                }

                // Set TrangThai based on day of week
                var dayOfWeek = holiday.NgayLe1.ToDateTime(TimeOnly.MinValue).DayOfWeek;
                holiday.TrangThai = holiday.TenNgayLe.Contains("Nghỉ bù") ? "NL3" :
                                   (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday) ? "NL2" : "NL1";

                _context.NgayLes.Add(holiday);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Thêm ngày lễ thành công.", holiday });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi thêm ngày lễ.", error = ex.Message });
            }
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Roles = "EMPLOYEE_MANAGER")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var holiday = await _context.NgayLes.FindAsync(id);
                if (holiday == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy ngày lễ." });
                }

                // Check if holiday management is allowed
                var currentDate = DateTime.Now;
                var currentYear = currentDate.Year;
                var isInPeriod = currentDate.Month == 12 && currentDate.Day >= 1 && currentDate.Day <= 30;
                var hasApprovedRequest = await _context.YeuCaus
                    .AnyAsync(y => y.TenYeuCau == "Yêu cầu mở duyệt ngày lễ" &&
                                   y.MoTa.Contains($"năm {currentYear}") &&
                                   y.TrangThai &&
                                   y.NgayDuyet.HasValue &&
                                   y.NgayDuyet.Value >= DateTime.Now.AddDays(-30));

                if (!isInPeriod && !hasApprovedRequest)
                {
                    return BadRequest(new { success = false, message = "Chức năng xóa ngày lễ chỉ được mở trong khoảng thời gian từ 1/12 đến 30/12 hoặc khi có yêu cầu được duyệt cho năm hiện tại." });
                }

                if (holiday.TrangThai is "NL4" or "NL5")
                    return Conflict(new { success = false, message = "Hủy duyệt ngày lễ trước khi xóa." });

                _context.NgayLes.Remove(holiday);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Xóa ngày lễ thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa ngày lễ.", error = ex.Message });
            }
        }

        [HttpGet("CheckOpenApprovalRequest")]
        public async Task<ActionResult> CheckOpenApprovalRequest(int year)
        {
            try
            {
                var exists = await _context.YeuCaus
                    .AnyAsync(y => y.TenYeuCau == "Yêu cầu mở duyệt ngày lễ" &&
                                   y.MoTa.Contains($"năm {year}") &&
                                   !y.TrangThai && y.NgayDuyet == null);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi kiểm tra yêu cầu.", error = ex.Message });
            }
        }

        [HttpGet("CheckApprovedRequest")]
        public async Task<ActionResult> CheckApprovedRequest(int year)
        {
            try
            {
                var exists = await _context.YeuCaus
                    .AnyAsync(y => y.TenYeuCau == "Yêu cầu mở duyệt ngày lễ" &&
                                   y.MoTa.Contains($"năm {year}") &&
                                   y.TrangThai &&
                                   y.NgayDuyet.HasValue &&
                                   y.NgayDuyet.Value >= DateTime.Now.AddDays(-30));
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi kiểm tra yêu cầu đã duyệt.", error = ex.Message });
            }
        }

        [HttpPost("RequestOpenApproval")]
        [Authorize(Roles = "EMPLOYEE_MANAGER")]
        public async Task<ActionResult> RequestOpenApproval([FromBody] YeuCau request)
        {
            try
            {
                // Validate input
                if (request.TenYeuCau != "Yêu cầu mở duyệt ngày lễ" || string.IsNullOrEmpty(request.MoTa))
                {
                    return BadRequest(new { success = false, message = "Yêu cầu không hợp lệ." });
                }

                // Check if a pending request already exists
                var yearMatch = System.Text.RegularExpressions.Regex.Match(request.MoTa, @"năm (\d{4})");
                if (!yearMatch.Success)
                {
                    return BadRequest(new { success = false, message = "Mô tả yêu cầu phải chứa năm cụ thể (e.g., năm 2025)." });
                }
                var year = int.Parse(yearMatch.Groups[1].Value);

                if (year is < 1900 or > 9999)
                    return BadRequest(new { success = false, message = "Năm đề nghị không hợp lệ." });

                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var existingRequest = await _context.YeuCaus
                    .AnyAsync(y => y.TenYeuCau == "Yêu cầu mở duyệt ngày lễ" &&
                                   y.MoTa.Contains($"năm {year}") &&
                                   !y.TrangThai && y.NgayDuyet == null);
                if (existingRequest)
                {
                    return BadRequest(new { success = false, message = $"Đã có yêu cầu mở duyệt đang chờ xử lý cho năm {year}." });
                }

                _context.YeuCaus.Add(new YeuCau
                {
                    TenYeuCau = request.TenYeuCau,
                    MoTa = request.MoTa.Trim(),
                    NgayTao = DateTime.Now,
                    MaNvTao = User.FindFirst("MaNV")?.Value
                });
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Yêu cầu đã được gửi thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi gửi yêu cầu.", error = ex.Message });
            }
        }
    }
}