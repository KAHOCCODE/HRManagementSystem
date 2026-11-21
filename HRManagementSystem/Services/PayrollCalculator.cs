using HR_KD.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HR_KD.Common;
using HR_KD.Services;

public class PayrollCalculator
{
    private readonly HrDbContext _context;
    private readonly IConfiguration _configuration;

    public PayrollCalculator(HrDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<decimal> CalculateRemainingLeaveDays(int employeeId, int year)
    {
        var leaveRecords = await _context.PhepNamNhanViens
            .Where(s => s.MaNv == employeeId && s.Nam <= year)
            .OrderByDescending(s => s.Nam)
            .Take(3)
            .ToListAsync();

        decimal remainingLeaveDays = 0;

        foreach (var record in leaveRecords)
        {
            remainingLeaveDays += record.SoNgayChuaSuDung;

            if (record.IsTinhLuong && record.Nam != year)
            {
                break;
            }
        }

        return remainingLeaveDays;
    }

    public async Task<BangLuong> CalculatePayroll(int employeeId, DateTime monthYear, List<ChamCong> attendanceRecords, ThongTinLuongNV salaryInfo)
    {
        var payroll = new BangLuong
        {
            MaNv = employeeId,
            ThangNam = DateOnly.FromDateTime(monthYear),
            TrangThai = PayrollStatus.Created
        };

        var startOfMonth = new DateOnly(monthYear.Year, monthYear.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        var contracts = await _context.HopDongLaoDongs
            .Include(hd => hd.LoaiHopDong)
            .Where(hd => hd.MaNv == employeeId && hd.NgayBatDau <= endOfMonth
                && (hd.NgayKetThuc == null || hd.NgayKetThuc >= startOfMonth)
                && (hd.NgayNghiViec == null || hd.NgayNghiViec >= startOfMonth))
            .Take(2)
            .ToListAsync();

        if (contracts.Count != 1)
            throw new InvalidOperationException("Kỳ lương cần đúng một hợp đồng có hiệu lực. Hãy đối soát hồ sơ hoặc tách kỳ có thay đổi.");

        var contract = contracts[0];
        if (await _context.ThongTinLuongNVs.AnyAsync(x => x.MaNv == employeeId
                && x.NgayApDung >= monthYear.AddDays(1) && x.NgayApDung < monthYear.AddMonths(1)))
            throw new InvalidOperationException("Có thay đổi lương giữa tháng. Cần phân đoạn kỳ lương trước khi tính.");

        decimal tiLeLuong = (decimal)(contract.LoaiHopDong.TiLeLuong ?? 1.0);

        var standardHoursRecord = await _context.GioChuans
            .FirstOrDefaultAsync(g => g.Nam == monthYear.Year && g.KichHoat == true);

        if (standardHoursRecord == null)
        {
            throw new Exception("Không tìm thấy thông tin giờ chuẩn cho tháng tính lương.");
        }

        decimal standardHours = monthYear.Month switch
        {
            1 => standardHoursRecord.Thang1,
            2 => standardHoursRecord.Thang2,
            3 => standardHoursRecord.Thang3,
            4 => standardHoursRecord.Thang4,
            5 => standardHoursRecord.Thang5,
            6 => standardHoursRecord.Thang6,
            7 => standardHoursRecord.Thang7,
            8 => standardHoursRecord.Thang8,
            9 => standardHoursRecord.Thang9,
            10 => standardHoursRecord.Thang10,
            11 => standardHoursRecord.Thang11,
            12 => standardHoursRecord.Thang12,
            _ => 0
        };

        var validAttendanceRecords = attendanceRecords
            .Where(a => a.TrangThai == AttendanceStatus.Approved || a.TrangThai == AttendanceStatus.Paidleave)
            .ToList();

        if (!validAttendanceRecords.Any())
        {
            throw new Exception("Không có bản ghi chấm công nào được duyệt (Approved hoặc Paidleave). Vui lòng yêu cầu quản lý duyệt hoặc không duyệt trạng thái chấm công.");
        }

        // Tính số giờ làm việc thực tế
        decimal actualHours = validAttendanceRecords.Sum(a => a.TongGio ?? 0);

        // Lấy cấu hình giờ làm việc mỗi ngày
        var workingHoursRecord = await _context.ChamCongGioRaVaos
            .Where(c => c.KichHoat == true)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync();

        if (workingHoursRecord == null)
        {
            throw new Exception("Không tìm thấy cấu hình giờ làm việc trong ChamCongGioRaVaos.");
        }

        decimal dailyWorkingHours = workingHoursRecord.TongGio;
        if (standardHours <= 0 || dailyWorkingHours <= 0)
            throw new InvalidOperationException("Giờ công chuẩn phải lớn hơn 0.");

        // Lấy dữ liệu thiếu/bù theo từng ngày trong tháng

        var timeRecords = await _context.TongGioThieus
            .Where(t => t.MaNv == employeeId &&
                        t.NgayBatDauThieu >= startOfMonth &&
                        t.NgayBatDauThieu <= endOfMonth)
            .ToListAsync();

        decimal makeupHours = timeRecords.Sum(t => t.TongGioLamBu);

        // Cộng giờ bù vào actualHours
        actualHours += makeupHours;

        // Tính lương cơ bản dựa trên số giờ làm việc thực tế
        decimal baseSalary = salaryInfo.LuongCoBan * (actualHours / standardHours) * tiLeLuong;

        // Tính lương tăng ca
        decimal hourlyRate = (salaryInfo.LuongCoBan * tiLeLuong) / standardHours;
        var overtimeRecords = await _context.TangCas
            .Where(t => t.MaNv == employeeId &&
                        t.NgayTangCa.Year == monthYear.Year &&
                        t.NgayTangCa.Month == monthYear.Month &&
                        t.TrangThai == OvertimeStatus.Approved)
            .ToListAsync();
        decimal overtimeSalary = overtimeRecords.Sum(t => (decimal)t.SoGioTangCa * hourlyRate * t.TyLeTangCa);

        decimal phuCapCoDinh = salaryInfo.PhuCapCoDinh ?? 0;
        decimal thuongCoDinh = salaryInfo.ThuongCoDinh ?? 0;
        decimal grossSalary = baseSalary + phuCapCoDinh + thuongCoDinh + overtimeSalary;

        decimal bhxh = salaryInfo.BHXH;
        decimal bhyt = salaryInfo.BHYT;
        decimal bhtn = salaryInfo.BHTN;

        decimal totalInsurance = bhxh + bhyt + bhtn;
        if (totalInsurance > grossSalary || bhxh < 0 || bhyt < 0 || bhtn < 0)
            throw new InvalidOperationException("Khoản bảo hiểm vượt lương gộp hoặc bị âm. Hãy đối soát kỳ lương.");

        var giamTruGiaCanh = await _context.GiamTruGiaCanhs
            .Where(g => g.NgayHieuLuc <= monthYear && (g.NgayHetHieuLuc == null || g.NgayHetHieuLuc > monthYear))
            .OrderByDescending(g => g.NgayHieuLuc)
            .FirstOrDefaultAsync();

        if (giamTruGiaCanh == null && monthYear.Year is not (2026 or 2027))
            throw new InvalidOperationException("Không tìm thấy thông tin giảm trừ gia cảnh hợp lệ.");

        var employee = await _context.NhanViens
            .FirstOrDefaultAsync(nv => nv.MaNv == employeeId);

        if (employee == null)
        {
            throw new Exception("Không tìm thấy thông tin nhân viên.");
        }

        decimal mucGiamTruBanThan = monthYear.Year is 2026 or 2027 ? 15500000m : giamTruGiaCanh!.MucGiamTruBanThan;
        decimal mucGiamTruNguoiPhuThuoc = monthYear.Year is 2026 or 2027 ? 6200000m : giamTruGiaCanh!.MucGiamTruNguoiPhuThuoc;
        int soNguoiPhuThuoc = employee.SoNguoiPhuThuoc;

        decimal giamTruTongCong = mucGiamTruBanThan + (soNguoiPhuThuoc * mucGiamTruNguoiPhuThuoc);
        decimal taxableIncome = grossSalary - bhxh - bhyt - bhtn - giamTruTongCong;

        decimal personalIncomeTax = 0;
        if (taxableIncome > 0)
        {
            personalIncomeTax = monthYear.Year is 2026 or 2027
                ? StatutoryPayroll.ResidentTax2026(taxableIncome)
                : await CalculatePersonalIncomeTax(taxableIncome);
        }

        decimal netSalary = grossSalary - bhxh - bhyt - bhtn - personalIncomeTax;
        if (netSalary < 0)
        {
            netSalary = 0;
        }

        payroll.TongLuong = grossSalary;
        payroll.LuongTangCa = overtimeSalary;
        payroll.PhuCapThem = 0;
        payroll.LuongThem = 0;
        payroll.ThueTNCN = personalIncomeTax; // Lưu giá trị dương. Xử lý hiển thị âm ở tầng giao diện nếu cần
        payroll.ThucNhan = netSalary;

        return payroll;
    }

    private async Task<decimal> CalculatePersonalIncomeTax(decimal taxableIncome)
    {
        if (taxableIncome <= 0) return 0;

        var taxBrackets = await _context.CauHinhLuongThues
            .OrderBy(t => t.MucLuongTu ?? 0)
            .ToListAsync();

        if (!taxBrackets.Any())
        {
            throw new Exception("Không tìm thấy cấu hình thuế TNCN trong bảng CauHinhLuongThue.");
        }

        decimal tax = 0;
        decimal remainingIncome = taxableIncome;

        foreach (var bracket in taxBrackets)
        {
            decimal from = bracket.MucLuongTu ?? 0;
            decimal to = bracket.MucLuongDen ?? decimal.MaxValue;
            decimal rate = bracket.GiaTri / 100;

            if (remainingIncome <= 0)
                break;

            decimal taxableInBracket = Math.Min(remainingIncome, to - from);
            if (taxableInBracket <= 0)
                continue;

            tax += taxableInBracket * rate;
            remainingIncome -= taxableInBracket;
        }

        if (remainingIncome > 0)
            throw new InvalidOperationException("Biểu thuế chưa bao phủ hết thu nhập tính thuế.");

        return tax;
    }

    public async Task<decimal?> ResolveMonthlyMinimumWage(int employeeId, DateTime effectiveDate)
    {
        var department = await _context.NhanViens.Where(x => x.MaNv == employeeId)
            .Select(x => (int?)x.MaPhongBan).SingleOrDefaultAsync();
        if (department == null)
            return null;

        // The existing employee model only records a department. Configure a region for a
        // department only when all employees in it share the same place of work.
        var configuredRegion = _configuration.GetValue<int?>($"HR:WorkRegionsByDepartment:{department}");
        var currentRegions = await _context.MucLuongToiThieuVungs
            .Where(x => x.IsActive).Take(2).ToListAsync();
        var region = configuredRegion ?? (currentRegions.Count == 1 ? currentRegions[0].VungLuong : null);
        if (region is not (>= 1 and <= 4))
            return null;

        var published = StatutoryPayroll.MinimumWage(DateOnly.FromDateTime(effectiveDate), region.Value);
        return published?.Month ?? (effectiveDate.Year < 2026 && currentRegions.Count == 1
            && currentRegions[0].VungLuong == region ? currentRegions[0].MucLuongToiThieuThang : null);
    }

    public async Task<(decimal BHXH, decimal BHYT, decimal BHTN, string ErrorMessage)> CalculateInsurance(int employeeId, DateTime effectiveDate, decimal luongCoBan, decimal? phuCapCoDinh, decimal? thuongCoDinh)
    {
        try
        {
            var minimumWage = await ResolveMonthlyMinimumWage(employeeId, effectiveDate);
            if (minimumWage == null)
                return (0, 0, 0, "Cần xác định vùng lương và văn bản có hiệu lực tại ngày áp dụng.");

            var referenceAmount = await _context.MucLuongCoSos
                .Where(x => x.NgayHieuLuc <= effectiveDate && (x.NgayHetHieuLuc == null || x.NgayHetHieuLuc > effectiveDate))
                .OrderByDescending(x => x.NgayHieuLuc).FirstOrDefaultAsync();
            if (referenceAmount == null)
                return (0, 0, 0, "Chưa có mức tham chiếu hoặc mức lương cơ sở tại ngày áp dụng.");

            var insuranceRates = await _context.ThongTinBaoHiems
                .Where(x => x.NgayHieuLuc <= effectiveDate && (x.NgayHetHieuLuc == null || x.NgayHetHieuLuc > effectiveDate))
                .ToListAsync();
            if (new[] { "BHXH", "BHYT", "BHTN" }.Any(type => insuranceRates.Count(x => x.LoaiBaoHiem == type) != 1))
                return (0, 0, 0, "Cần đúng một tỷ lệ cho từng loại bảo hiểm tại ngày áp dụng.");

            var bhxhRate = insuranceRates.FirstOrDefault(b => b.LoaiBaoHiem == "BHXH")?.TyLeNguoiLaoDong ?? 0;
            var bhytRate = insuranceRates.FirstOrDefault(b => b.LoaiBaoHiem == "BHYT")?.TyLeNguoiLaoDong ?? 0;
            var bhtnRate = insuranceRates.FirstOrDefault(b => b.LoaiBaoHiem == "BHTN")?.TyLeNguoiLaoDong ?? 0;

            var additions = phuCapCoDinh.GetValueOrDefault() + thuongCoDinh.GetValueOrDefault();
            var insurableAdditions = _configuration.GetValue<decimal?>(
                $"HR:InsurableAdditionsByEmployee:{employeeId}:{effectiveDate:yyyy-MM}");
            if ((additions > 0 && insurableAdditions == null) || insurableAdditions < 0 || insurableAdditions > additions)
                return (0, 0, 0, "Cần khai số tiền bổ sung tính đóng bảo hiểm đã đối soát cho nhân viên và tháng áp dụng.");
            var luongDongBaoHiem = luongCoBan + insurableAdditions.GetValueOrDefault();

            var mucLuongToiThieuVung = minimumWage.Value;
            var mucLuongToiDaBHXH_BHYT = referenceAmount.LuongCoSo * 20;
            var mucLuongToiDaBHTN = mucLuongToiThieuVung * 20;

            if (luongDongBaoHiem < mucLuongToiThieuVung)
            {
                return (0, 0, 0, $"Mức lương dùng để tính bảo hiểm phải lớn hơn hoặc bằng mức lương tối thiểu vùng: {mucLuongToiThieuVung:N0} VNĐ.");
            }

            var luongDongBHXH = Math.Min(luongDongBaoHiem, mucLuongToiDaBHXH_BHYT);
            var luongDongBHYT = Math.Min(luongDongBaoHiem, mucLuongToiDaBHXH_BHYT);
            var luongDongBHTN = Math.Min(luongDongBaoHiem, mucLuongToiDaBHTN);

            var bhxh = luongDongBHXH * (bhxhRate / 100);
            var bhyt = luongDongBHYT * (bhytRate / 100);
            var bhtn = luongDongBHTN * (bhtnRate / 100);

            if (bhxh < 0 || bhyt < 0 || bhtn < 0)
            {
                return (0, 0, 0, "Các khoản bảo hiểm không được âm.");
            }

            return (bhxh, bhyt, bhtn, null);
        }
        catch (Exception ex)
        {
            return (0, 0, 0, $"Lỗi khi tính toán bảo hiểm: {ex.Message}");
        }
    }
}