using System.Security.Cryptography;
using System.Text;
using HR_KD.Common;
using HR_KD.Services;
using Microsoft.EntityFrameworkCore;

namespace HR_KD.Data;

internal static class DemoData
{
    internal static readonly DateOnly DataThrough = new(2026, 11, 20);
    private static readonly DateOnly StartOfYear = new(2026, 1, 1);
    private static readonly TimeOnly ShiftStart = new(8, 0);
    private static readonly TimeOnly ShiftEnd = new(17, 0);
    internal static async Task InitializeAsync(HrDbContext db, PayrollCalculator calculator, string? password, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16 || Encoding.UTF8.GetByteCount(password) > 48)
        {
            throw new InvalidOperationException("Demo:Password must be at least 16 characters and at most 48 UTF-8 bytes.");
        }

        if (!await db.Database.EnsureCreatedAsync())
            await AddWorksiteTablesToExistingDemoAsync(db);
        await using var seedTransaction = await db.Database.BeginTransactionAsync();
        if (await db.TaiKhoans.AnyAsync())
        {
            return;
        }


        var headquarters = new Worksite
        {
            Code = "HCM-SAIGON", Name = "Văn phòng Sài Gòn",
            Ward = "Phường Sài Gòn", Address = "Đường Nguyễn Huệ", Region2026 = 1, Region2027 = 1
        };
        var canGio = new Worksite
        {
            Code = "HCM-CANGIO", Name = "Chi nhánh Cần Giờ",
            Ward = "Xã Cần Giờ", Address = "Đường Rừng Sác", Region2026 = 2, Region2027 = 1
        };
        var department = new PhongBan { TenPhongBan = "Vận hành", MoTa = "Vận hành nội bộ" };
        var leaveType = new LoaiNgayNghi
        {
            TenLoai = "Nghỉ phép năm",
            HuongLuong = true,
            TinhVaoPhepNam = true
        };
        var leavePolicy = new CauHinhPhepNam { Nam = DataThrough.Year, SoNgayPhepMacDinh = 12 };
        db.Set<CauHinhPhep_ChinhSach>().Add(new CauHinhPhep_ChinhSach
        {
            CauHinhPhepNam = leavePolicy,
            ChinhSachPhepNam = new ChinhSachPhepNam
            {
                TenChinhSach = "Thêm 1 ngày khi đủ 5 năm", SoNam = 5, SoNgayCongThem = 1,
                ApDungTuNam = DataThrough.Year, ConHieuLuc = true
            }
        });

        var signedContracts = new LoaiHopDong { MaLoaiHopDong = 2, TenLoaiHopDong = "Không xác định thời hạn", TiLeLuong = 1 };
        db.LoaiHopDongs.AddRange(
            new LoaiHopDong { MaLoaiHopDong = 1, TenLoaiHopDong = "Xác định thời hạn", ThoiHanMacDinh = 36, TiLeLuong = 1, GiaHanToiDa = 1 },
            signedContracts);
        var schedule = new LichLamViec { Nam = DataThrough.Year, TenLich = "T2 - T6", KichHoat = true };
        db.LichLamViecs.AddRange(schedule,
            new LichLamViec { TenLich = "T2 - Nửa T7", Nam = DataThrough.Year },
            new LichLamViec { TenLich = "T2 - T7", Nam = DataThrough.Year });
        var staff = new List<(string Username, NhanVien Employee, ThongTinLuongNV Salary, List<ChamCong> Attendance)>();
        var sampleUsers = new[]
        {
            (Username: "demo.director", Name: "Nguyễn Minh An", Role: "DIRECTOR", Title: "Giám đốc", MonthlyPay: 36000000m),
            (Username: "demo.hr", Name: "Trần Khánh Linh", Role: "EMPLOYEE_MANAGER", Title: "Quản lý nhân sự", MonthlyPay: 24000000m),
            (Username: "demo.manager", Name: "Lê Hoàng Nam", Role: "LINE_MANAGER", Title: "Trưởng phòng", MonthlyPay: 21000000m),
            (Username: "demo.payroll", Name: "Phạm Gia Hân", Role: "PAYROLL_AUDITOR", Title: "Kế toán lương", MonthlyPay: 16000000m),
            (Username: "demo.employee", Name: "Võ Thanh Bình", Role: "EMPLOYEE", Title: "Nhân viên", MonthlyPay: 12000000m)
        };
        foreach (var sample in sampleUsers)
        {
            var employee = new NhanVien
            {
                HoTen = sample.Name,
                Email = sample.Username + "@example.invalid",
                NgayVaoLam = StartOfYear.AddYears(-1),
                MaPhongBanNavigation = department,
                MaChucVuNavigation = new ChucVu { TenChucVu = sample.Title }
            };
            db.WorksiteAssignments.Add(new WorksiteAssignment
            {
                Employee = employee,
                Worksite = sample.Role == "EMPLOYEE" ? canGio : headquarters,
                EffectiveFrom = StartOfYear
            });
            var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
            var account = new TaiKhoan
            {
                Username = sample.Username,
                PasswordSalt = salt,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password + salt),
                MaNvNavigation = employee
            };
            account.TaiKhoanQuyenHans.Add(new TaiKhoanQuyenHan
            {
                QuyenHan = new QuyenHan
                {
                    MaQuyenHan = sample.Role,
                    TenQuyenHan = sample.Title,
                    MoTaQuyenHan = "Quyền theo chức danh"
                }
            });
            db.TaiKhoans.Add(account);

            db.HopDongLaoDongs.Add(new HopDongLaoDong
            {
                NhanVien = employee, LoaiHopDong = signedContracts, NgayBatDau = StartOfYear,
                GhiChu = "Hợp đồng lao động"
            });

            var salary = new ThongTinLuongNV
            {
                NhanVien = employee, LuongCoBan = sample.MonthlyPay, NgayApDung = StartOfYear.ToDateTime(TimeOnly.MinValue),
                BHXH = sample.MonthlyPay * .08m, BHYT = sample.MonthlyPay * .015m, BHTN = sample.MonthlyPay * .01m,
                IsActive = true, GhiChu = "Lương theo hợp đồng"
            };
            db.ThongTinLuongNVs.Add(salary);
            var attendance = new List<ChamCong>();
            staff.Add((sample.Username, employee, salary, attendance));
            var annualLeaveUsed = sample.Role == "EMPLOYEE" ? 2 : 0;
            db.SoDuPheps.Add(new SoDuPhep
            {
                MaNvNavigation = employee, Nam = DataThrough.Year,
                NgayCapNhat = DataThrough.ToDateTime(ShiftEnd), SoNgayConLai = 12 - annualLeaveUsed
            });
            db.PhepNamNhanViens.Add(new PhepNamNhanVien
            {
                MaNvNavigation = employee, CauHinhPhepNam = leavePolicy, Nam = DataThrough.Year,
                NgayCapNhat = DataThrough.ToDateTime(ShiftEnd), SoNgayPhepDuocCap = 12,
                SoNgayDaSuDung = annualLeaveUsed, SoNgayChuaSuDung = 12 - annualLeaveUsed
            });
        }

        db.ChamCongGioRaVaos.Add(new ChamCongGioRaVao
        {
            GioVao = ShiftStart, GioRa = ShiftEnd, TongGio = 8, KichHoat = true
        });
        db.TiLeTangCas.AddRange(
            new TiLeTangCa { TenTiLeTangCa = "Ngày thường", TiLe = 150, KichHoat = true },
            new TiLeTangCa { TenTiLeTangCa = "Ngày nghỉ hằng tuần", TiLe = 200, KichHoat = true },
            new TiLeTangCa { TenTiLeTangCa = "Ngày lễ", TiLe = 300, KichHoat = true });

        AddPayrollConfiguration(db);
        AddStatuses(db);

        var sampleHolidays = new (DateOnly Start, string Name, int Length)[]
        {
            (StartOfYear, "Tết Dương lịch", 1),
            (new(2026, 2, 16), "Tết Nguyên đán", 5),
            (new(2026, 4, 26), "Giỗ Tổ Hùng Vương", 1),
            (new(2026, 4, 27), "Nghỉ bù Giỗ Tổ Hùng Vương", 1),
            (new(2026, 4, 30), "Ngày Giải phóng miền Nam", 1),
            (new(2026, 5, 1), "Quốc tế Lao động", 1),
            (new(2026, 9, 1), "Quốc khánh", 2)
        };
        var holidayDays = sampleHolidays.SelectMany(holiday =>
            Enumerable.Range(0, holiday.Length).Select(offset => holiday.Start.AddDays(offset))).ToHashSet();
        foreach (var holiday in sampleHolidays)
        {
            db.NgayLes.Add(new NgayLe { TenNgayLe = holiday.Name, NgayLe1 = holiday.Start, SoNgayNghi = holiday.Length, TrangThai = "NL4" });
        }

        foreach (var holiday in sampleHolidays.Where(x => x.Start.Month != 2 && x.Start.Day != 26 && x.Start.Day != 27))
            db.NgayLeCoDinhs.Add(new NgayLeCoDinh { TenNgayLe = holiday.Name, NgayLe1 = holiday.Start, SoNgayNghi = holiday.Length });

        await db.SaveChangesAsync();
        db.GioChuans.Add(BuildStandardHours(schedule.Id));
        var director = staff.Single(x => x.Username == "demo.director").Employee;
        var sampleEmployee = staff.Single(x => x.Username == "demo.employee").Employee;
        var transferEmployee = staff.Single(x => x.Username == "demo.manager").Employee;
        db.WorksiteAssignments.Add(new WorksiteAssignment
        {
            Employee = transferEmployee, Worksite = canGio, EffectiveFrom = new DateOnly(2026, 10, 1)
        });

        var approvedLeave = new[] { new DateOnly(2026, 8, 14), new DateOnly(2026, 11, 13) };
        AddLeave(db, sampleEmployee, leaveType, director, approvedLeave[0], "DEMO-001", LeaveStatus.Approved);
        AddLeave(db, sampleEmployee, leaveType, director, approvedLeave[1], "DEMO-002", LeaveStatus.Approved);
        AddLeave(db, sampleEmployee, leaveType, director, DataThrough, "DEMO-003", LeaveStatus.Pending);
        db.LoaiNgayNghis.AddRange(
            new LoaiNgayNghi { TenLoai = "Nghỉ việc riêng hưởng lương", HuongLuong = true },
            new LoaiNgayNghi { TenLoai = "Nghỉ không hưởng lương", HuongLuong = false });

        for (var date = StartOfYear; date <= DataThrough; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            foreach (var account in staff)
            {
                if (account.Employee == sampleEmployee && date == DataThrough)
                    continue;

                var paidLeave = account.Employee == sampleEmployee && approvedLeave.Contains(date);
                var holiday = holidayDays.Contains(date);
                var entry = new ChamCong
                {
                    MaNv = account.Employee.MaNv, NgayLamViec = date,
                    GioVao = paidLeave || holiday ? null : ShiftStart, GioRa = paidLeave || holiday ? null : ShiftEnd,
                    TongGio = 8, TrangThai = paidLeave || holiday ? AttendanceStatus.Paidleave : AttendanceStatus.Approved,
                    GhiChu = paidLeave ? "Phép năm đã duyệt" : holiday ? "Ngày lễ hưởng lương" : null,
                    MaNvDuyet = director.MaNv, NgayDuyet = date.ToDateTime(new TimeOnly(18, 0))
                };
                db.ChamCongs.Add(entry);
                account.Attendance.Add(entry);
            }
        }

        db.TangCas.AddRange(
            new TangCa { MaNv = sampleEmployee.MaNv, NgayTangCa = new(2026, 10, 16), SoGioTangCa = 2, TyLeTangCa = 1.5m,
                GioVao = ShiftEnd, GioRa = new(19, 0), TrangThai = OvertimeStatus.Approved, MaNvDuyet = director.MaNv, GhiChu = "Hỗ trợ vận hành cuối ngày" },
            new TangCa { MaNv = sampleEmployee.MaNv, NgayTangCa = new(2026, 11, 19), SoGioTangCa = 1, TyLeTangCa = 1.5m,
                GioVao = ShiftEnd, GioRa = new(18, 0), TrangThai = OvertimeStatus.Pending, GhiChu = "Bàn giao cuối ca" });
        await db.SaveChangesAsync();

        for (var month = StartOfYear; month.Month < DataThrough.Month; month = month.AddMonths(1))
        {
            foreach (var account in staff)
            {
                var period = month.ToDateTime(TimeOnly.MinValue);
                var pay = await calculator.CalculatePayroll(account.Employee.MaNv, period,
                    account.Attendance.Where(entry => entry.NgayLamViec.Month == month.Month).ToList(), account.Salary);
                pay.NgayTao = month.AddMonths(1).ToDateTime(new TimeOnly(9, 0));
                pay.NguoiTao = "demo.payroll";

                pay.TrangThai = month.Month == 10 ? account.Username switch
                {
                    "demo.hr" => PayrollStatus.SentToAccounting,
                    "demo.manager" => PayrollStatus.AccountingApproved,
                    "demo.payroll" => PayrollStatus.DirectorApproved,
                    _ => PayrollStatus.SentToEmployee
                } : PayrollStatus.SentToEmployee;
                db.BangLuongs.Add(pay);
            }
        }

        await db.SaveChangesAsync();
        await seedTransaction.CommitAsync();
        logger.LogInformation("Demo data seeded through {Cutoff:yyyy-MM-dd}: {Employees} employees, 2 worksites, {Attendance} attendance entries, {Payrolls} payrolls, 3 leave entries.",
            DataThrough, staff.Count, staff.Sum(x => x.Attendance.Count), staff.Count * (DataThrough.Month - 1));
    }

    private static void AddLeave(HrDbContext db, NhanVien employee, LoaiNgayNghi leaveType,
        NhanVien director, DateOnly date, string code, string status)
    {
        var submitted = date.AddDays(-3).ToDateTime(new TimeOnly(9, 0));
        db.NgayNghis.Add(new NgayNghi
        {
            MaNvNavigation = employee, MaLoaiNgayNghiNavigation = leaveType, NgayNghi1 = date,
            NgayLamDon = submitted, NgayDuyet = submitted, NguoiDuyetId = status == LeaveStatus.Approved ? director.MaNv : null,
            MaTrangThai = status, MaDon = code, LyDo = "Nghỉ phép cá nhân"
        });
    }

    private static GioChuan BuildStandardHours(int scheduleId)
    {
        int Hours(int month)
        {
            var start = new DateOnly(DataThrough.Year, month, 1);
            return Enumerable.Range(0, DateTime.DaysInMonth(start.Year, month))
                .Count(day => start.AddDays(day).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) * 8;
        }

        return new GioChuan
        {
            Nam = DataThrough.Year, IdLichLamViec = scheduleId, KichHoat = true, Thang1 = Hours(1), Thang2 = Hours(2),
            Thang3 = Hours(3), Thang4 = Hours(4), Thang5 = Hours(5), Thang6 = Hours(6), Thang7 = Hours(7),
            Thang8 = Hours(8), Thang9 = Hours(9), Thang10 = Hours(10), Thang11 = Hours(11), Thang12 = Hours(12)
        };
    }

    private static void AddPayrollConfiguration(HrDbContext db)
    {
        var effective = StartOfYear.ToDateTime(TimeOnly.MinValue);
        for (var region = 1; region <= 4; region++)
        {
            var wage = StatutoryPayroll.MinimumWage(StartOfYear, region)!.Value;
            db.MucLuongToiThieuVungs.Add(new MucLuongToiThieuVung
            {
                VungLuong = region, MucLuongToiThieuThang = wage.Month, MucLuongToiThieuGio = wage.Hour, IsActive = true
            });
        }

        var july = new DateTime(2026, 7, 1);
        db.MucLuongCoSos.AddRange(
            new MucLuongCoSo { LuongCoSo = 2340000, NgayHieuLuc = effective, NgayHetHieuLuc = july, GhiChu = "Tham số demo", IsActive = false },
            new MucLuongCoSo { LuongCoSo = 2530000, NgayHieuLuc = july, GhiChu = "Tham số demo", IsActive = true });
        db.GiamTruGiaCanhs.Add(new GiamTruGiaCanh
        {
            MucGiamTruBanThan = 15500000, MucGiamTruNguoiPhuThuoc = 6200000, NgayHieuLuc = effective,
            GhiChu = "Thông số kỳ tính thuế 2026"
        });
        foreach (var item in new (decimal From, decimal? To, decimal Rate)[]
            { (0, 10000000, 5), (10000000, 30000000, 10), (30000000, 60000000, 20), (60000000, 100000000, 30), (100000000, null, 35) })
        {
            db.CauHinhLuongThues.Add(new CauHinhLuongThue
            {
                MucLuongTu = item.From, MucLuongDen = item.To, GiaTri = item.Rate, MoTa = "Biểu thuế năm 2026"
            });
        }

        foreach (var item in new[] { ("BHXH", 8m, 17.5m), ("BHYT", 1.5m, 3m), ("BHTN", 1m, 1m) })
            db.ThongTinBaoHiems.Add(new ThongTinBaoHiem
            {
                LoaiBaoHiem = item.Item1, TyLeNguoiLaoDong = item.Item2, TyLeNhaTuyenDung = item.Item3,
                NgayHieuLuc = effective, GhiChu = "Tỷ lệ đóng bảo hiểm"
            });
    }

    private static void AddStatuses(HrDbContext db)
    {
        foreach (var item in new (string Code, string Name)[]
        {
            ("CC1", "Chờ duyệt"), ("CC2", "Đã duyệt cấp quản lý"), ("CC3", "Đã duyệt"), ("CC4", "Từ chối"),
            ("CC5", "Nghỉ hưởng lương"), ("CC6", "Nghỉ không lương"),
            ("NN1", "Chờ duyệt"), ("NN2", "Đã duyệt"), ("NN3", "Từ chối"), ("NN4", "Đã hủy"),
            ("NN5", "Đã duyệt nghỉ không lương"), ("NN6", "Cần bổ sung"),
            ("TC1", "Chờ duyệt"), ("TC2", "Đã duyệt cấp quản lý"), ("TC3", "Đã duyệt"), ("TC4", "Từ chối"),
            ("LS1", "Chờ duyệt"), ("LS2", "Đã duyệt cấp quản lý"), ("LS3", "Đã duyệt"), ("LS4", "Từ chối"),
            ("BL1", "Đã tạo"), ("BL1A", "Nhân viên xác nhận"), ("BL1R", "Nhân viên từ chối"),
            ("BL2", "Chờ kế toán"), ("BL2R", "Kế toán trả về"), ("BL3", "Chờ giám đốc"),
            ("BL3R", "Giám đốc trả về"), ("BL4", "Giám đốc đã duyệt"), ("BL5", "Đã gửi nhân viên"),
            ("NL1", "Chờ duyệt"), ("NL2", "Trùng ngày nghỉ hằng tuần"), ("NL3", "Nghỉ bù"),
            ("NL4", "Đã duyệt"), ("NL5", "Đã duyệt ngày nghỉ hằng tuần"), ("NL6", "Từ chối")
        })
            db.TrangThais.Add(new TrangThai { MaTrangThai = item.Code, TenTrangThai = item.Name });
    }

    private static async Task AddWorksiteTablesToExistingDemoAsync(HrDbContext db)
    {
        // Demo databases are created with EnsureCreated and have no EF migration history.
        // Add only the new tables so a local SQLite demo keeps its existing records.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS Worksites (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL,
                Name TEXT NOT NULL,
                Province TEXT NOT NULL,
                Ward TEXT NOT NULL,
                Address TEXT NOT NULL,
                Region2026 INTEGER NULL,
                Region2027 INTEGER NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Worksites_Code ON Worksites (Code);

            CREATE TABLE IF NOT EXISTS WorksiteAssignments (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL REFERENCES NhanVien (MaNV) ON DELETE RESTRICT,
                WorksiteId INTEGER NOT NULL REFERENCES Worksites (Id) ON DELETE RESTRICT,
                EffectiveFrom TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_WorksiteAssignments_EmployeeId_EffectiveFrom
                ON WorksiteAssignments (EmployeeId, EffectiveFrom);
            CREATE INDEX IF NOT EXISTS IX_WorksiteAssignments_WorksiteId ON WorksiteAssignments (WorksiteId);
            """);
        await transaction.CommitAsync();
    }
}
