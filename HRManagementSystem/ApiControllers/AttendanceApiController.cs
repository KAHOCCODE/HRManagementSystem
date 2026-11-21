using HR_KD.Data;
using HR_KD.DTOs;
using HR_KD.Common;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HR_KD.ApiControllers
{
    [Route("api/Attendance")]
    [ApiController]
    public class AttendanceController : ControllerBase
    {
        private readonly HrDbContext _context;

        public AttendanceController(HrDbContext context)
        {
            _context = context;
        }

        private int? GetMaNvFromClaims()
        {
            var maNvClaim = User.FindFirst("MaNV")?.Value;
            return int.TryParse(maNvClaim, out int maNv) ? maNv : null;
        }

        private (DateOnly start, DateOnly end) GetWeekRange(DateOnly date)
        {
            DateTime dateTime = date.ToDateTime(TimeOnly.MinValue);
            int dayOfWeek = (int)dateTime.DayOfWeek;
            int daysToMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
            DateOnly start = date.AddDays(-daysToMonday);
            DateOnly end = start.AddDays(6);
            return (start, end);
        }

        [HttpGet("GetOvertimeRates")]
        public async Task<IActionResult> GetOvertimeRates()
        {
            try
            {
                var rates = await _context.TiLeTangCas
                    .Where(t => t.KichHoat)
                    .Select(t => new { t.Id, t.TenTiLeTangCa, t.TiLe })
                    .ToListAsync();
                return Ok(new { success = true, rates });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống.", error = ex.Message });
            }
        }

        [HttpPost]
        [Route("SubmitAttendance")]
        public async Task<IActionResult> SubmitAttendance(AttendanceDataDTO data)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            if (data == null || (data.attendance == null || !data.attendance.Any()) && (data.overtime == null || !data.overtime.Any()))
            {
                return BadRequest(new { success = false, message = "Dữ liệu chấm công không hợp lệ." });
            }

            var employeeId = maNv.Value;
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var hours = await _context.ChamCongGioRaVaos.Where(x => x.KichHoat)
                .OrderByDescending(x => x.Id).Select(x => x.TongGio).FirstOrDefaultAsync();
            if (hours <= 0 || hours > 10)
                return BadRequest(new { message = "Cần thiết lập giờ công chuẩn hợp lệ trước khi chấm công." });

            var activeSchedule = await _context.LichLamViecs.Where(x => x.KichHoat)
                .OrderByDescending(x => x.Id).Select(x => x.TenLich).FirstOrDefaultAsync();
            if (activeSchedule is not ("T2 - T6" or "T2 - Nửa T7" or "T2 - T7"))
                return BadRequest(new { message = "Cần kích hoạt lịch làm việc trước khi chấm công." });

            var regularEntries = new List<LichSuChamCong>();
            var extraEntries = new List<TangCa>();
            foreach (var entry in data.attendance ?? [])
            {
                if (!DateOnly.TryParse(entry.NgayLamViec, out var date)
                    || !TimeOnly.TryParse(entry.GioVao, out var from)
                    || !TimeOnly.TryParse(entry.GioRa, out var to)
                    || !TryGetDaytimeHours(from, to, out var duration))
                    return BadRequest(new { message = "Giờ công không hợp lệ hoặc cần xử lý riêng cho ca đêm, ca qua ngày." });

                var dailyLimit = ScheduledHours(date, activeSchedule, hours);
                if (duration > dailyLimit || await IsHoliday(date))
                    return BadRequest(new { message = $"Giờ công ngày {date:dd/MM/yyyy} vượt lịch làm hoặc rơi vào ngày lễ." });

                if (regularEntries.Any(x => x.Ngay == date)
                    || await _context.LichSuChamCongs.AnyAsync(x => x.MaNv == employeeId && x.Ngay == date))
                    return BadRequest(new { message = $"Đã có bản ghi chấm công ngày {date:dd/MM/yyyy}." });

                if (await _context.NgayNghis.AnyAsync(x => x.MaNv == employeeId && x.NgayNghi1 == date
                    && (x.MaTrangThai == LeaveStatus.Approved || x.MaTrangThai == "NN5")))
                    return BadRequest(new { message = $"Ngày {date:dd/MM/yyyy} đã có đơn nghỉ được duyệt." });

                var (start, end) = GetWeekRange(date);
                var existingHours = await _context.LichSuChamCongs
                    .Where(x => x.MaNv == employeeId && x.Ngay >= start && x.Ngay <= end
                        && x.TrangThai != AttendanceHistoryStatus.Rejected)
                    .SumAsync(x => x.TongGio ?? 0);
                var pendingHours = regularEntries.Where(x => x.Ngay >= start && x.Ngay <= end)
                    .Sum(x => x.TongGio ?? 0);
                if (existingHours + pendingHours + duration > 48)
                    return BadRequest(new { message = "Tổng giờ làm việc bình thường vượt 48 giờ trong tuần." });

                regularEntries.Add(new LichSuChamCong
                {
                    MaNv = employeeId, Ngay = date, GioVao = from, GioRa = to,
                    TongGio = duration, TrangThai = AttendanceHistoryStatus.Pending, GhiChu = entry.GhiChu
                });
            }

            foreach (var entry in data.overtime ?? [])
            {
                if (!DateOnly.TryParse(entry.NgayTangCa, out var date) || date.Year >= 9999
                    || !TimeOnly.TryParse(entry.GioVaoTangCa, out var from)
                    || !TimeOnly.TryParse(entry.GioRaTangCa, out var to)
                    || !TryGetDaytimeHours(from, to, out var duration))
                    return BadRequest(new { message = "Giờ tăng ca không hợp lệ hoặc cần xử lý riêng cho ca đêm, ca qua ngày." });

                var dailyHours = ScheduledHours(date, activeSchedule, hours);
                var isHoliday = await IsHoliday(date);
                var minimumPercent = isHoliday ? 300 : dailyHours == 0 ? 200 : 150;
                var rate = await _context.TiLeTangCas.Where(x => x.KichHoat && x.TiLe == entry.TiLeTangCa)
                    .Select(x => (double?)x.TiLe).FirstOrDefaultAsync();
                if (rate == null || rate < minimumPercent)
                    return BadRequest(new { message = $"Chọn tỷ lệ đã kích hoạt từ {minimumPercent}% cho ngày {date:dd/MM/yyyy}." });

                var yearStart = new DateOnly(date.Year, 1, 1);
                var yearEnd = yearStart.AddYears(1);
                var existing = await _context.TangCas.Where(x => x.MaNv == employeeId && x.NgayTangCa >= yearStart
                    && x.NgayTangCa < yearEnd && x.TrangThai != OvertimeStatus.Rejected).ToListAsync();
                var counted = existing.Concat(extraEntries.Where(x => x.NgayTangCa.Year == date.Year)).ToList();
                var sameDay = counted.Where(x => x.NgayTangCa == date).ToList();
                if (sameDay.Any(x => x.GioVao < to && x.GioRa > from)
                    || regularEntries.Any(x => x.Ngay == date && x.GioVao < to && x.GioRa > from)
                    || await _context.LichSuChamCongs.AnyAsync(x => x.MaNv == employeeId && x.Ngay == date
                        && x.TrangThai != AttendanceHistoryStatus.Rejected && x.GioVao < to && x.GioRa > from)
                    || await _context.LamBus.AnyAsync(x => x.MaNV == employeeId && x.NgayLamViec == date
                        && x.GioVao < to && x.GioRa > from))
                    return BadRequest(new { message = $"Giờ tăng ca ngày {date:dd/MM/yyyy} bị trùng với bản ghi khác." });

                var maxDaily = isHoliday || dailyHours == 0 ? 12 : Math.Min(dailyHours / 2, 12 - dailyHours);
                if (sameDay.Sum(x => x.SoGioTangCa) + duration > maxDaily
                    || counted.Where(x => x.NgayTangCa.Month == date.Month).Sum(x => x.SoGioTangCa) + duration > 40
                    || counted.Sum(x => x.SoGioTangCa) + duration > 200)
                    return BadRequest(new { message = "Số giờ tăng ca vượt giới hạn ngày, tháng hoặc năm đang áp dụng." });

                extraEntries.Add(new TangCa
                {
                    MaNv = employeeId, NgayTangCa = date, SoGioTangCa = duration,
                    TyLeTangCa = (decimal)rate.Value / 100, GioVao = from, GioRa = to,
                    TrangThai = OvertimeStatus.Pending, GhiChu = entry.GhiChu
                });
            }

            _context.LichSuChamCongs.AddRange(regularEntries);
            _context.TangCas.AddRange(extraEntries);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { success = true, message = "Đã lưu chấm công." });
        }

        private static decimal ScheduledHours(DateOnly date, string schedule, decimal fullDay) =>
            date.DayOfWeek switch
            {
                DayOfWeek.Sunday => 0,
                DayOfWeek.Saturday when schedule == "T2 - T6" => 0,
                DayOfWeek.Saturday when schedule == "T2 - Nửa T7" => fullDay / 2,
                _ => fullDay
            };

        private static bool TryGetDaytimeHours(TimeOnly from, TimeOnly to, out decimal duration)
        {
            duration = 0;
            if (from < new TimeOnly(6, 0) || to > new TimeOnly(22, 0) || to <= from)
                return false;

            // The existing web form uses a noon break. Deduct only the portion inside this entry.
            var breakStart = new TimeOnly(12, 0);
            var breakEnd = new TimeOnly(13, 0);
            var overlapStart = from > breakStart ? from : breakStart;
            var overlapEnd = to < breakEnd ? to : breakEnd;
            var overlap = overlapEnd > overlapStart ? overlapEnd - overlapStart : TimeSpan.Zero;
            duration = Math.Round((decimal)((to - from) - overlap).TotalHours, 2);
            return duration > 0;
        }

        private async Task<bool> IsHoliday(DateOnly date)
        {
            var holidays = await _context.NgayLes.Where(x => x.NgayLe1 <= date
                && (x.TrangThai == "NL4" || x.TrangThai == "NL5")).ToListAsync();
            return holidays.Any(x => date.DayNumber - x.NgayLe1.DayNumber < (x.SoNgayNghi ?? 1));
        }

        [HttpGet]
        [Route("GetOvertimeRecords")]
        public async Task<IActionResult> GetOvertimeRecords(string? ngayTangCa = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            var query = _context.TangCas.Where(c => c.MaNv == maNv.Value);

            if (!string.IsNullOrEmpty(ngayTangCa) && DateOnly.TryParse(ngayTangCa, out DateOnly ngay))
            {
                query = query.Where(c => c.NgayTangCa == ngay);
            }

            var records = await query
                .Select(c => new
                {
                    c.NgayTangCa,
                    c.SoGioTangCa,
                    c.TrangThai
                })
                .ToListAsync();

            return Ok(new { success = true, records });
        }

        [HttpGet]
        [Route("GetAttendanceRecords")]
        public async Task<IActionResult> GetAttendanceRecords(string? ngayLamViec = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            var query = _context.ChamCongs.Where(c => c.MaNv == maNv.Value && c.TrangThai == "CC2");

            if (!string.IsNullOrEmpty(ngayLamViec) && DateOnly.TryParse(ngayLamViec, out DateOnly ngay))
            {
                query = query.Where(c => c.NgayLamViec == ngay);
            }

            var records = await query
                .Select(c => new
                {
                    c.NgayLamViec,
                    GioVao = c.GioVao.HasValue ? c.GioVao.Value.ToString("HH:mm") : null,
                    GioRa = c.GioRa.HasValue ? c.GioRa.Value.ToString("HH:mm") : null,
                    c.TongGio,
                    c.TrangThai,
                    c.GhiChu
                })
                .ToListAsync();

            return Ok(new { success = true, records });
        }

        [HttpGet]
        [Route("GetAttendanceHistoryRecords")]
        public async Task<IActionResult> GetAttendanceHistoryRecords(string? ngayLamViec = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            var query = _context.LichSuChamCongs.Where(c => c.MaNv == maNv.Value);

            if (!string.IsNullOrEmpty(ngayLamViec) && DateOnly.TryParse(ngayLamViec, out DateOnly ngay))
            {
                query = query.Where(c => c.Ngay == ngay);
            }

            var records = await query
                .Select(c => new
                {
                    c.Ngay,
                    GioVao = c.GioVao.HasValue ? c.GioVao.Value.ToString("HH:mm") : null,
                    GioRa = c.GioRa.HasValue ? c.GioRa.Value.ToString("HH:mm") : null,
                    c.TongGio,
                    c.TrangThai,
                    c.GhiChu
                })
                .ToListAsync();

            return Ok(new { success = true, records });
        }

        [HttpGet]
        [Route("GetAttendanceHistoryRecords2")]
        public async Task<IActionResult> GetAttendanceHistoryRecords2(string? ngayLamViec = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            var query = _context.LichSuChamCongs.Where(c => c.MaNv == maNv.Value && c.TrangThai == "LS1");

            if (!string.IsNullOrEmpty(ngayLamViec) && DateOnly.TryParse(ngayLamViec, out DateOnly ngay))
            {
                query = query.Where(c => c.Ngay == ngay);
            }

            var records = await query
                .Select(c => new
                {
                    c.Ngay,
                    GioVao = c.GioVao.HasValue ? c.GioVao.Value.ToString("HH:mm") : null,
                    GioRa = c.GioRa.HasValue ? c.GioRa.Value.ToString("HH:mm") : null,
                    c.TongGio,
                    c.TrangThai,
                    c.GhiChu
                })
                .ToListAsync();

            return Ok(new { success = true, records });
        }

        [HttpGet("GetAttendanceRequests")]
        public async Task<IActionResult> GetAttendanceRequests(string? ngayLamViec = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            var query = _context.YeuCauSuaChamCongs.Where(c => c.MaNv == maNv.Value);

            if (!string.IsNullOrEmpty(ngayLamViec) && DateOnly.TryParse(ngayLamViec, out DateOnly ngay))
            {
                query = query.Where(c => c.NgayLamViec == ngay);
            }

            var requests = await query
                .Select(c => new
                {
                    c.MaYeuCau,
                    c.NgayLamViec,
                    GioVaoMoi = c.GioVaoMoi.HasValue ? c.GioVaoMoi.Value.ToString("HH:mm") : null,
                    GioRaMoi = c.GioRaMoi.HasValue ? c.GioRaMoi.Value.ToString("HH:mm") : null,
                    c.LyDo,
                    c.TrangThai
                })
                .ToListAsync();

            return Ok(new { success = true, requests });
        }

        [HttpPost("AcceptAttendanceRequest")]
        public async Task<IActionResult> AcceptAttendanceRequest(AcceptAttendanceRequestDTO request)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            var yeuCau = await _context.YeuCauSuaChamCongs
                .FirstOrDefaultAsync(y => y.MaYeuCau == request.MaYeuCau && y.MaNv == maNv.Value);

            if (yeuCau == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy yêu cầu chấm công." });
            }

            try
            {
                foreach (var entry in request.AttendanceData)
                {
                    if (!DateOnly.TryParse(entry.NgayLamViec, out var ngayLamViec))
                    {
                        return BadRequest(new { success = false, message = $"Ngày làm việc không hợp lệ: {entry.NgayLamViec}" });
                    }

                    var chamCong = await _context.ChamCongs
                        .FirstOrDefaultAsync(c => c.MaNv == maNv.Value && c.NgayLamViec == ngayLamViec);

                    if (chamCong == null)
                    {
                        chamCong = new ChamCong
                        {
                            MaNv = maNv.Value,
                            NgayLamViec = ngayLamViec,
                            TrangThai = "CC2",
                            GhiChu = "Chấp nhận yêu cầu chấm công"
                        };
                        _context.ChamCongs.Add(chamCong);
                    }

                    chamCong.GioVao = yeuCau.GioVaoMoi ?? chamCong.GioVao;
                    chamCong.GioRa = yeuCau.GioRaMoi ?? chamCong.GioRa;

                    if (chamCong.GioVao.HasValue && chamCong.GioRa.HasValue)
                    {
                        chamCong.TongGio = (decimal)(chamCong.GioRa.Value - chamCong.GioVao.Value).TotalHours;
                    }
                }

                _context.YeuCauSuaChamCongs.Remove(yeuCau);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Xử lý yêu cầu thành công",
                    deletedRequestId = yeuCau.MaYeuCau
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi hệ thống",
                    error = ex.Message
                });
            }
        }

        [HttpGet("GetStatusForMonth")]
        public async Task<IActionResult> GetStatusForMonth(int maNV, string? monthYear = null)
        {
            DateTime selectedMonth = string.IsNullOrEmpty(monthYear)
                ? DateTime.Now
                : DateTime.TryParseExact(monthYear, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out DateTime parsedMonth)
                    ? parsedMonth
                    : DateTime.Now;

            var records = await _context.ChamCongs
                .Where(c => c.MaNv == maNV &&
                            c.NgayLamViec.Year == selectedMonth.Year &&
                            c.NgayLamViec.Month == selectedMonth.Month &&
                            c.TrangThai.Trim() == "CC2")
                .ToListAsync();

            if (!records.Any())
            {
                var historyRecords = await _context.LichSuChamCongs
                    .Where(c => c.MaNv == maNV &&
                                c.Ngay.Year == selectedMonth.Year &&
                                c.Ngay.Month == selectedMonth.Month &&
                                c.TrangThai.Trim() == "LS2")
                    .ToListAsync();

                if (!historyRecords.Any())
                {
                    return Ok(new { status = "Chưa có dữ liệu chấm công đã duyệt" });
                }

                var historyApprovedCount = historyRecords.Count;
                var historyTotalDays = historyRecords.Count;
                return Ok(new { status = $"Lịch sử: {historyApprovedCount}/{historyTotalDays} ngày đã duyệt" });
            }

            var approvedCount = records.Count;
            var totalDays = records.Count;

            return Ok(new { status = $"{approvedCount}/{totalDays} ngày đã duyệt" });
        }

        [HttpPost("RejectAttendanceRequest")]
        public async Task<IActionResult> RejectAttendanceRequest(RejectAttendanceRequestDTO request)
        {
            try
            {
                var requestRecord = await _context.YeuCauSuaChamCongs
                    .FirstOrDefaultAsync(a => a.MaYeuCau == request.MaYeuCau);

                if (requestRecord == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy yêu cầu chấm công." });
                }

                if (request.NgayLamViec == default(DateOnly) || request.NgayLamViec == DateOnly.MinValue)
                {
                    return BadRequest(new { success = false, message = "Ngày làm việc không hợp lệ." });
                }

                var ngayNghi = new NgayNghi
                {
                    MaNv = requestRecord.MaNv,
                    NgayNghi1 = request.NgayLamViec,
                    LyDo = !string.IsNullOrEmpty(request.LyDo) ? request.LyDo : "Không có lý do cụ thể",
                    MaLoaiNgayNghi = request.MaLoaiNgayNghi,
                    NgayLamDon = DateTime.Now
                };

                _context.NgayNghis.Add(ngayNghi);
                await _context.SaveChangesAsync();

                _context.YeuCauSuaChamCongs.Remove(requestRecord);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Yêu cầu chấm công đã bị từ chối và lưu vào NgayNghi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống.", error = ex.Message });
            }
        }

        [HttpGet("GetHolidays")]
        public async Task<IActionResult> GetHolidays()
        {
            try
            {
                var holidays = await _context.NgayLes
                    .Where(n => n.TrangThai == "NL4")
                    .Select(n => new
                    {
                        ngayLe = n.NgayLe1.ToString("yyyy-MM-dd"),
                        tenNgayLe = n.TenNgayLe,
                        trangThai = n.TrangThai,
                        soNgayNghi = n.SoNgayNghi
                    })
                    .ToListAsync();

                return Ok(new { success = true, holidays });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống.", error = ex.Message });
            }
        }

        [HttpGet]
        [Route("GetAttendanceCalendarRecords")]
        public async Task<IActionResult> GetAttendanceCalendarRecords(string? ngayLamViec = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            var query = _context.ChamCongs.Where(c => c.MaNv == maNv.Value && (c.TrangThai == "CC3"));

            if (!string.IsNullOrEmpty(ngayLamViec) && DateOnly.TryParse(ngayLamViec, out DateOnly ngay))
            {
                query = query.Where(c => c.NgayLamViec == ngay);
            }

            var records = await query
                .Select(c => new
                {
                    c.NgayLamViec,
                    GioVao = c.GioVao.HasValue ? c.GioVao.Value.ToString("HH:mm") : null,
                    GioRa = c.GioRa.HasValue ? c.GioRa.Value.ToString("HH:mm") : null,
                    c.TongGio,
                    c.TrangThai,
                    c.GhiChu
                })
                .ToListAsync();

            return Ok(new { success = true, records });
        }

        [HttpGet]
        [Route("GetApprovedRecords")]
        public async Task<IActionResult> GetApprovedRecords(string? startDate = null, string? endDate = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            try
            {
                // Define the base queries with aligned anonymous types
                var chamCongQuery = _context.ChamCongs
                    .Where(c => c.MaNv == maNv.Value && c.TrangThai == "CC3")
                    .Select(c => new
                    {
                        Loai = "Chấm Công",
                        Ngay = c.NgayLamViec, // DateOnly
                        GioVao = c.GioVao, // TimeOnly?
                        GioRa = c.GioRa, // TimeOnly?
                        TongGio = c.TongGio, // decimal?
                        GhiChu = c.GhiChu ?? "", // string
                        TrangThai = c.TrangThai // string
                    });

                var tangCaQuery = _context.TangCas
                    .Where(c => c.MaNv == maNv.Value && c.TrangThai == "TC3")
                    .Select(c => new
                    {
                        Loai = "Tăng Ca",
                        Ngay = c.NgayTangCa, // DateOnly
                        GioVao = c.GioVao, // TimeOnly?
                        GioRa = c.GioRa, // TimeOnly?
                        TongGio = (decimal?)c.SoGioTangCa, // Cast to decimal? to align types
                        GhiChu = c.GhiChu ?? "", // string
                        TrangThai = c.TrangThai // string
                    });

                var lamBuQuery = _context.LamBus
                    .Where(c => c.MaNV == maNv.Value && c.TrangThai == "LB3")
                    .Select(c => new
                    {
                        Loai = "Làm Bù",
                        Ngay = c.NgayLamViec, // DateOnly
                        GioVao = c.GioVao, // TimeOnly?
                        GioRa = c.GioRa, // TimeOnly?
                        TongGio = c.TongGio, // decimal?
                        GhiChu = c.GhiChu ?? "", // string
                        TrangThai = c.TrangThai // string
                    });

                // Combine queries with Union
                var query = chamCongQuery
                    .Union(tangCaQuery)
                    .Union(lamBuQuery);

                // Apply date filtering if provided
                if (DateOnly.TryParse(startDate, out DateOnly start) && DateOnly.TryParse(endDate, out DateOnly end))
                {
                    query = query.Where(r => r.Ngay >= start && r.Ngay <= end);
                }

                // Project into DTO after Union
                var records = await query
                    .OrderBy(r => r.Ngay)
                    .Select(r => new AttendanceRecordDTO
                    {
                        Loai = r.Loai,
                        Ngay = r.Ngay.ToString("yyyy-MM-dd"),
                        GioVao = r.GioVao.HasValue ? r.GioVao.Value.ToString("HH:mm") : null,
                        GioRa = r.GioRa.HasValue ? r.GioRa.Value.ToString("HH:mm") : null,
                        TongGio = r.TongGio,
                        GhiChu = r.GhiChu,
                        TrangThai = r.TrangThai
                    })
                    .ToListAsync();

                return Ok(new { success = true, records });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống.", error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        [Route("GetPendingRecords")]
        public async Task<IActionResult> GetPendingRecords(string? startDate = null, string? endDate = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            try
            {
                // Define the base queries with aligned anonymous types
                var chamCongQuery = _context.LichSuChamCongs
                    .Where(c => c.MaNv == maNv.Value && c.TrangThai == "LS1")
                    .Select(c => new
                    {
                        Loai = "Chấm Công",
                        Ngay = c.Ngay, // Ensure DateOnly
                        GioVao = c.GioVao, // TimeOnly?
                        GioRa = c.GioRa, // TimeOnly?
                        TongGio = c.TongGio, // decimal?
                        GhiChu = c.GhiChu ?? "", // string
                        TrangThai = c.TrangThai // string
                    });

                var tangCaQuery = _context.TangCas
                    .Where(c => c.MaNv == maNv.Value && c.TrangThai == "TC1")
                    .Select(c => new
                    {
                        Loai = "Tăng Ca",
                        Ngay = c.NgayTangCa, // Ensure DateOnly
                        GioVao = c.GioVao, // TimeOnly?
                        GioRa = c.GioRa, // TimeOnly?
                        TongGio = (decimal?)c.SoGioTangCa, // Cast to decimal? to align types
                        GhiChu = c.GhiChu ?? "", // string
                        TrangThai = c.TrangThai // string
                    });

                var lamBuQuery = _context.LamBus
                    .Where(c => c.MaNV == maNv.Value && c.TrangThai == "LB1")
                    .Select(c => new
                    {
                        Loai = "Làm Bù",
                        Ngay = c.NgayLamViec, // Ensure DateOnly
                        GioVao = c.GioVao, // TimeOnly?
                        GioRa = c.GioRa, // TimeOnly?
                        TongGio = c.TongGio, // decimal?
                        GhiChu = c.GhiChu ?? "", // string
                        TrangThai = c.TrangThai // string
                    });

                // Combine queries with Union
                var query = chamCongQuery
                    .Union(tangCaQuery)
                    .Union(lamBuQuery);

                // Apply date filtering if provided
                if (DateOnly.TryParse(startDate, out DateOnly start) && DateOnly.TryParse(endDate, out DateOnly end))
                {
                    query = query.Where(r => r.Ngay >= start && r.Ngay <= end);
                }

                // Project into DTO after Union
                var records = await query
                    .OrderBy(r => r.Ngay)
                    .Select(r => new AttendanceRecordDTO
                    {
                        Loai = r.Loai,
                        Ngay = r.Ngay.ToString("yyyy-MM-dd"),
                        GioVao = r.GioVao.HasValue ? r.GioVao.Value.ToString("HH:mm") : null,
                        GioRa = r.GioRa.HasValue ? r.GioRa.Value.ToString("HH:mm") : null,
                        TongGio = r.TongGio,
                        GhiChu = r.GhiChu,
                        TrangThai = r.TrangThai
                    })
                    .ToListAsync();

                return Ok(new { success = true, records });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống.", error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        [Route("GetRejectedRecords")]
        public async Task<IActionResult> GetRejectedRecords(string? startDate = null, string? endDate = null)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            try
            {
                // Define the base queries with aligned anonymous types
                var lichsuchamCongQuery = _context.LichSuChamCongs
                    .Where(c => c.MaNv == maNv.Value && c.TrangThai == "LS4")
                    .Select(c => new
                    {
                        Loai = "Chấm Công lần 1",
                        Ngay = c.Ngay,
                        GioVao = c.GioVao,
                        GioRa = c.GioRa,
                        TongGio = c.TongGio,
                        GhiChu = c.GhiChu ?? "",
                        TrangThai = c.TrangThai
                    });

                var chamCongQuery = _context.ChamCongs
                    .Where(c => c.MaNv == maNv.Value && c.TrangThai == "CC4")
                    .Select(c => new
                    {
                        Loai = "Chấm Công",
                        Ngay = c.NgayLamViec,
                        GioVao = c.GioVao,
                        GioRa = c.GioRa,
                        TongGio = c.TongGio,
                        GhiChu = c.GhiChu ?? "",
                        TrangThai = c.TrangThai
                    });

                var tangCaQuery = _context.TangCas
                    .Where(c => c.MaNv == maNv.Value && c.TrangThai == "TC4")
                    .Select(c => new
                    {
                        Loai = "Tăng Ca",
                        Ngay = c.NgayTangCa,
                        GioVao = c.GioVao,
                        GioRa = c.GioRa,
                        TongGio = (decimal?)c.SoGioTangCa,
                        GhiChu = c.GhiChu ?? "",
                        TrangThai = c.TrangThai
                    });

                var lamBuQuery = _context.LamBus
                    .Where(c => c.MaNV == maNv.Value && c.TrangThai == "LB4")
                    .Select(c => new
                    {
                        Loai = "Làm Bù",
                        Ngay = c.NgayLamViec,
                        GioVao = c.GioVao,
                        GioRa = c.GioRa,
                        TongGio = c.TongGio,
                        GhiChu = c.GhiChu ?? "",
                        TrangThai = c.TrangThai
                    });

                // Combine queries with Union
                var query = lichsuchamCongQuery
                    .Union(chamCongQuery)
                    .Union(tangCaQuery)
                    .Union(lamBuQuery);

                // Apply date filtering if provided
                if (DateOnly.TryParse(startDate, out DateOnly start) && DateOnly.TryParse(endDate, out DateOnly end))
                {
                    query = query.Where(r => r.Ngay >= start && r.Ngay <= end);
                }

                // Project into DTO after Union
                var records = await query
                    .OrderBy(r => r.Ngay)
                    .Select(r => new AttendanceRecordDTO
                    {
                        Loai = r.Loai,
                        Ngay = r.Ngay.ToString("yyyy-MM-dd"),
                        GioVao = r.GioVao.HasValue ? r.GioVao.Value.ToString("HH:mm") : null,
                        GioRa = r.GioRa.HasValue ? r.GioRa.Value.ToString("HH:mm") : null,
                        TongGio = r.TongGio,
                        GhiChu = r.GhiChu,
                        TrangThai = r.TrangThai
                    })
                    .ToListAsync();

                return Ok(new { success = true, records });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống.", error = ex.Message });
            }
        }
        [HttpDelete("DeleteRejectedRecord")]
        public async Task<IActionResult> DeleteRejectedRecord([FromBody] DeleteRecordRequest request)
        {
            var maNv = GetMaNvFromClaims();
            if (!maNv.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không xác định được nhân viên." });
            }

            try
            {
                if (!DateOnly.TryParse(request.Ngay, out DateOnly ngay))
                {
                    return BadRequest(new { success = false, message = "Định dạng ngày không hợp lệ." });
                }

                int rowsAffected = 0;

                switch (request.Loai)
                {
                    case "Chấm Công lần 1":
                        var lichSuChamCong = await _context.LichSuChamCongs
                            .FirstOrDefaultAsync(c => c.MaNv == maNv.Value && c.Ngay == ngay && c.TrangThai == "LS4");
                        if (lichSuChamCong != null)
                        {
                            _context.LichSuChamCongs.Remove(lichSuChamCong);
                            rowsAffected = await _context.SaveChangesAsync();
                        }
                        break;

                    case "Chấm Công":
                        var chamCong = await _context.ChamCongs
                            .FirstOrDefaultAsync(c => c.MaNv == maNv.Value && c.NgayLamViec == ngay && c.TrangThai == "CC4");
                        if (chamCong != null)
                        {
                            _context.ChamCongs.Remove(chamCong);
                            rowsAffected = await _context.SaveChangesAsync();

                            // Kiểm tra và xóa bản ghi trong LichSuChamCong
                            var lichSuChamCongToDelete = await _context.LichSuChamCongs
                                .FirstOrDefaultAsync(c => c.MaNv == maNv.Value && c.Ngay == ngay);
                            if (lichSuChamCongToDelete != null)
                            {
                                _context.LichSuChamCongs.Remove(lichSuChamCongToDelete);
                                rowsAffected += await _context.SaveChangesAsync();
                            }
                        }
                        break;

                    case "Tăng Ca":
                        var tangCa = await _context.TangCas
                            .FirstOrDefaultAsync(c => c.MaNv == maNv.Value && c.NgayTangCa == ngay && c.TrangThai == "TC4");
                        if (tangCa != null)
                        {
                            _context.TangCas.Remove(tangCa);
                            rowsAffected = await _context.SaveChangesAsync();
                        }
                        break;

                    case "Làm Bù":
                        var lamBu = await _context.LamBus
                            .FirstOrDefaultAsync(c => c.MaNV == maNv.Value && c.NgayLamViec == ngay && c.TrangThai == "LB4");
                        if (lamBu != null)
                        {
                            _context.LamBus.Remove(lamBu);
                            rowsAffected = await _context.SaveChangesAsync();
                        }
                        break;

                    default:
                        return BadRequest(new { success = false, message = "Loại bản ghi không hợp lệ." });
                }

                if (rowsAffected > 0)
                {
                    return Ok(new { success = true, message = "Xóa bản ghi thành công." });
                }
                else
                {
                    return NotFound(new { success = false, message = "Không tìm thấy bản ghi để xóa." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống.", error = ex.Message });
            }
        }

        public class DeleteRecordRequest
        {
            public string Loai { get; set; }
            public string Ngay { get; set; }
        }

    }
}