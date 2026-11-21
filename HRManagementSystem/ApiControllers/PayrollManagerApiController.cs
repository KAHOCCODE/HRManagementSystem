using HR_KD.Data;
using HR_KD.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HR_KD.Common;

namespace HR_KD.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = PayrollAccess.ReviewRoles)]
    public class PayrollManagerApiController : ControllerBase
    {
        private readonly HrDbContext _context;
        private readonly Dictionary<string, string> _statusNameMapping;

        public PayrollManagerApiController(HrDbContext context)
        {
            _context = context;
            _statusNameMapping = _context.TrangThais.ToDictionary(t => t.MaTrangThai, t => t.TenTrangThai);
        }

        #region API cho Trưởng phòng
        [HttpGet("GetDepartmentPayrolls")]
        public async Task<IActionResult> GetDepartmentPayrolls(int year, int month)
        {
            if (year is < 1 or > 9999 || month is < 1 or > 12)
                return BadRequest("Tháng hoặc năm không hợp lệ.");

            var bangLuongs = await PayrollAccess.DepartmentPayrolls(_context, User)
                .Include(b => b.MaNvNavigation).ThenInclude(nv => nv.MaPhongBanNavigation)
                .Where(b => b.ThangNam.Year == year && b.ThangNam.Month == month)
                .ToListAsync();

            var result = bangLuongs.Select(b => new
            {
                b.MaLuong,
                b.MaNv,
                HoTen = b.MaNvNavigation.HoTen,
                TenPhongBan = b.MaNvNavigation.MaPhongBanNavigation.TenPhongBan,
                b.ThucNhan,
                b.TrangThai,
                b.GhiChu,
                TenTrangThai = _statusNameMapping.TryGetValue(b.TrangThai ?? "", out var name) ? name : b.TrangThai
            });

            return Ok(result);
        }

        [Authorize(Roles = PayrollAccess.LineManager)]
        [HttpPost("SendToAccountant")]
        public async Task<IActionResult> SendToAccountant([FromBody] int[] maLuongList)
        {
            var luongs = await PayrollAccess.DepartmentPayrolls(_context, User, forUpdate: true)
                .Where(b => maLuongList.Contains(b.MaLuong) && b.TrangThai == "BL1A")
                .ToListAsync();

            if (luongs.Count != maLuongList.Length)
                return BadRequest("Một hoặc nhiều bảng lương không ở trạng thái phù hợp để gửi.");

            var currentUser = User.Identity?.Name;
            var currentTime = DateTime.Now;

            foreach (var b in luongs)
            {
                b.TrangThai = "BL2"; // Gửi lên kế toán
                b.NguoiGuiKT = currentUser;
                b.NgayGuiKT = currentTime;
            }

            await _context.SaveChangesAsync();
            return Ok("Bảng lương đã được gửi lên kế toán thành công.");
        }

        [HttpGet("GetUnconfirmedPayrolls")]
        public async Task<IActionResult> GetUnconfirmedPayrolls(int year, int month)
        {
            if (year is < 1 or > 9999 || month is < 1 or > 12)
                return BadRequest("Tháng hoặc năm không hợp lệ.");

            var bangLuongs = await PayrollAccess.DepartmentPayrolls(_context, User)
                .Include(b => b.MaNvNavigation).ThenInclude(nv => nv.MaPhongBanNavigation)
                .Where(b => b.ThangNam.Year == year && b.ThangNam.Month == month
                         && b.TrangThai == "BL1")
                .ToListAsync();

            var result = bangLuongs.Select(b => new
            {
                b.MaLuong,
                b.MaNv,
                HoTen = b.MaNvNavigation.HoTen,
                TenPhongBan = b.MaNvNavigation.MaPhongBanNavigation.TenPhongBan,
                b.ThucNhan,
                b.TrangThai,
                TenTrangThai = _statusNameMapping.TryGetValue(b.TrangThai ?? "", out var name) ? name : b.TrangThai
            });

            return Ok(result);
        }

        [Authorize(Roles = PayrollAccess.LineManager)]
        [HttpPost("MarkUnconfirmedAsConfirmed")]
        public async Task<IActionResult> MarkUnconfirmedAsConfirmed([FromBody] int[] maLuongList)
        {
            var luongs = await PayrollAccess.DepartmentPayrolls(_context, User, forUpdate: true)
                .Where(b => maLuongList.Contains(b.MaLuong) && b.TrangThai == "BL1")
                .ToListAsync();

            if (luongs.Count != maLuongList.Length)
                return BadRequest("Một hoặc nhiều bảng lương không ở trạng thái 'Đã tạo'.");

            var currentUser = User.Identity?.Name;
            var currentTime = DateTime.Now;

            foreach (var b in luongs)
            {
                b.TrangThai = "BL1A"; // Nhân viên xác nhận (do trưởng phòng đánh dấu)
                b.NguoiDuyetNV = currentUser;
                b.NgayDuyetNV = currentTime;
                b.GhiChu = b.GhiChu != null ? $"{b.GhiChu}\nTrưởng phòng xác nhận thay nhân viên." : "Trưởng phòng xác nhận thay nhân viên.";
            }

            await _context.SaveChangesAsync();
            return Ok("Bảng lương chưa xác nhận đã được đánh dấu là xác nhận.");
        }

        [HttpGet("GetEmployeeRevisionRequests")]
        public async Task<IActionResult> GetEmployeeRevisionRequests(int year, int month)
        {
            if (year is < 1 or > 9999 || month is < 1 or > 12)
                return BadRequest("Tháng hoặc năm không hợp lệ.");

            var bangLuongs = await PayrollAccess.DepartmentPayrolls(_context, User)
                .Include(b => b.MaNvNavigation).ThenInclude(nv => nv.MaPhongBanNavigation)
                .Where(b => b.ThangNam.Year == year && b.ThangNam.Month == month
                         && b.TrangThai == "BL1R")
                .ToListAsync();

            var result = bangLuongs.Select(b => new
            {
                b.MaLuong,
                b.MaNv,
                HoTen = b.MaNvNavigation.HoTen,
                TenPhongBan = b.MaNvNavigation.MaPhongBanNavigation.TenPhongBan,
                b.ThucNhan,
                b.TrangThai,
                b.GhiChu,
                TenTrangThai = _statusNameMapping.TryGetValue(b.TrangThai ?? "", out var name) ? name : b.TrangThai
            });

            return Ok(result);
        }

        [Authorize(Roles = PayrollAccess.LineManager)]
        [HttpPost("HandleEmployeeRevisionRequest/{maLuong}")]
        public async Task<IActionResult> HandleEmployeeRevisionRequest(int maLuong, [FromBody] RevisionRequestAction action)
        {
            var payroll = await PayrollAccess.DepartmentPayrolls(_context, User, forUpdate: true)
                .Include(b => b.MaNvNavigation)
                .FirstOrDefaultAsync(b => b.MaLuong == maLuong && b.TrangThai == "BL1R");

            if (payroll == null)
                return NotFound("Không tìm thấy bảng lương hoặc bảng lương không ở trạng thái yêu cầu chỉnh sửa.");

            var currentUser = User.Identity?.Name;
            var currentTime = DateTime.Now;

            if (action.Action == "APPROVE")
            {
                payroll.TrangThai = "BL2"; // Quay lại trạng thái "Đã tạo" để nhân viên xác nhận lại
                payroll.GhiChu = $"{payroll.GhiChu}\nTrưởng phòng phê duyệt yêu cầu chỉnh sửa: {action.Reason}.";
            }
            else if (action.Action == "REJECT")
            {
                payroll.TrangThai = "BL1A"; // Xác nhận thay nhân viên
                payroll.NguoiDuyetNV = currentUser;
                payroll.NgayDuyetNV = currentTime;
                payroll.GhiChu = $"{payroll.GhiChu}\nTrưởng phòng từ chối yêu cầu chỉnh sửa: {action.Reason}.";
            }
            else
            {
                return BadRequest("Hành động không hợp lệ. Vui lòng chọn 'APPROVE' hoặc 'REJECT'.");
            }

            await _context.SaveChangesAsync();
            return Ok($"Yêu cầu chỉnh sửa đã được xử lý: {action.Action}.");
        }

        [HttpGet("GetAccountantRejections")]
        public async Task<IActionResult> GetAccountantRejections(int year, int month)
        {
            if (year is < 1 or > 9999 || month is < 1 or > 12)
                return BadRequest("Tháng hoặc năm không hợp lệ.");

            var bangLuongs = await PayrollAccess.DepartmentPayrolls(_context, User)
                .Include(b => b.MaNvNavigation).ThenInclude(nv => nv.MaPhongBanNavigation)
                .Where(b => b.ThangNam.Year == year && b.ThangNam.Month == month
                         && b.TrangThai == "BL2R")
                .ToListAsync();

            var result = bangLuongs.Select(b => new
            {
                b.MaLuong,
                b.MaNv,
                HoTen = b.MaNvNavigation.HoTen,
                TenPhongBan = b.MaNvNavigation.MaPhongBanNavigation.TenPhongBan,
                b.ThucNhan,
                b.TrangThai,
                b.GhiChu,
                TenTrangThai = _statusNameMapping.TryGetValue(b.TrangThai ?? "", out var name) ? name : b.TrangThai
            });

            return Ok(result);
        }

        [Authorize(Roles = PayrollAccess.LineManager)]
        [HttpPost("ResendToAccountant/{maLuong}")]
        public async Task<IActionResult> ResendToAccountant(int maLuong, [FromBody] string adjustmentNote)
        {
            var payroll = await PayrollAccess.DepartmentPayrolls(_context, User, forUpdate: true)
                .FirstOrDefaultAsync(b => b.MaLuong == maLuong && b.TrangThai == "BL2R");

            if (payroll == null)
                return NotFound("Không tìm thấy bảng lương hoặc bảng lương không ở trạng thái bị kế toán trả về.");

            var currentUser = User.Identity?.Name;
            var currentTime = DateTime.Now;

            payroll.TrangThai = "BL2"; // Gửi lại lên kế toán
            payroll.NguoiGuiKT = currentUser;
            payroll.NgayGuiKT = currentTime;
            payroll.GhiChu = $"{payroll.GhiChu}\nTrưởng phòng gửi lại sau chỉnh sửa: {adjustmentNote}.";

            await _context.SaveChangesAsync();
            return Ok("Bảng lương đã được gửi lại lên kế toán.");
        }
        #endregion
    }
}