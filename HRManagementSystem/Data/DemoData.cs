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
            await EnsureDemoExtensionsAsync(db);
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
        var thuDuc = new Worksite
        {
            Code = "HCM-THUDUC", Name = "Trung tâm khai thác Thủ Đức",
            Ward = "Phường Thủ Đức", Address = "Đường Võ Văn Ngân", Region2026 = 1, Region2027 = 1
        };
        var sites = new Dictionary<string, Worksite> { ["SG"] = headquarters, ["CG"] = canGio, ["TD"] = thuDuc };
        var departments = DemoRoster.Team.Select(x => x.Department).Distinct()
            .ToDictionary(x => x, x => new PhongBan { TenPhongBan = x });
        var positions = DemoRoster.Team.Select(x => x.Position).Distinct()
            .ToDictionary(x => x, x => new ChucVu { TenChucVu = x });
        var leaveDates = new (int Index, DateOnly Date, string Status, string Reason)[]
        {
            (4, new(2026, 8, 14), LeaveStatus.Approved, "Việc gia đình"),
            (4, new(2026, 11, 13), LeaveStatus.Approved, "Việc gia đình"),
            (4, DataThrough, LeaveStatus.Pending, "Việc cá nhân"),
            (5, new(2026, 9, 18), LeaveStatus.Approved, "Nghỉ phép cá nhân"),
            (5, new(2026, 11, 18), LeaveStatus.Pending, "Việc gia đình"),
            (8, new(2026, 10, 9), LeaveStatus.Approved, "Việc cá nhân"),
            (11, new(2026, 11, 5), LeaveStatus.Approved, "Việc gia đình"),
            (12, new(2026, 11, 9), LeaveStatus.Rejected, "Việc gia đình"),
            (13, new(2026, 9, 25), LeaveStatus.Approved, "Nghỉ phép cá nhân"),
            (15, new(2026, 10, 19), LeaveStatus.Approved, "Việc gia đình"),
            (17, new(2026, 11, 10), LeaveStatus.Approved, "Việc cá nhân"),
            (19, new(2026, 10, 26), LeaveStatus.Approved, "Việc gia đình"),
            (21, new(2026, 11, 16), LeaveStatus.Pending, "Việc gia đình"),
            (22, new(2026, 9, 15), LeaveStatus.Approved, "Việc cá nhân"),
            (23, new(2026, 11, 6), LeaveStatus.Approved, "Nghỉ phép cá nhân")
        };
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
        var fixedTermContract = new LoaiHopDong { MaLoaiHopDong = 1, TenLoaiHopDong = "Xác định thời hạn",
            ThoiHanMacDinh = 12, TiLeLuong = 1, GiaHanToiDa = 1 };
        db.LoaiHopDongs.AddRange(fixedTermContract, signedContracts);
        var schedule = new LichLamViec { Nam = DataThrough.Year, TenLich = "T2 - T6", KichHoat = true };
        db.LichLamViecs.AddRange(schedule,
            new LichLamViec { TenLich = "T2 - Nửa T7", Nam = DataThrough.Year },
            new LichLamViec { TenLich = "T2 - T7", Nam = DataThrough.Year });
        var staff = new List<(NhanVien Employee, ThongTinLuongNV Salary, List<ChamCong> Attendance)>();
        var roles = DemoRoster.Team.Where(x => x.Role != null).ToDictionary(x => x.Role!, x =>
            new QuyenHan { MaQuyenHan = x.Role!, TenQuyenHan = x.Position, MoTaQuyenHan = x.Position });
        db.QuyenHans.AddRange(roles.Values);
        for (var index = 0; index < DemoRoster.Team.Length; index++)
        {
            var person = DemoRoster.Team[index];
            var site = sites[person.Site];
            var payrollStart = person.Hired > StartOfYear ? person.Hired : StartOfYear;
            var employee = new NhanVien
            {
                HoTen = person.Name,
                Email = (person.Login ?? $"nv{index + 1:00}") + "@example.invalid",
                GioiTinh = person.Male,
                NgaySinh = new DateOnly(person.BirthYear, index % 12 + 1, index % 22 + 1),
                DiaChi = $"{site.Ward}, TP.HCM",
                NgayVaoLam = person.Hired,
                MaPhongBanNavigation = departments[person.Department],
                MaChucVuNavigation = positions[person.Position],
                SoNguoiPhuThuoc = person.Dependents,
                AvatarUrl = $"/images/employees/an-phu-nam/{index + 1:00}.webp"
            };
            db.NhanViens.Add(employee);
            db.WorksiteAssignments.Add(new WorksiteAssignment
            {
                Employee = employee, Worksite = site, EffectiveFrom = payrollStart
            });
            if (person.Login != null)
            {
                var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
                var account = new TaiKhoan
                {
                    Username = person.Login, PasswordSalt = salt,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password + salt),
                    MaNvNavigation = employee
                };
                account.TaiKhoanQuyenHans.Add(new TaiKhoanQuyenHan { QuyenHan = roles[person.Role!] });
                db.TaiKhoans.Add(account);
            }

            bool fixedTerm = person.Hired.Year == DataThrough.Year;
            db.HopDongLaoDongs.Add(new HopDongLaoDong
            {
                NhanVien = employee, LoaiHopDong = fixedTerm ? fixedTermContract : signedContracts,
                NgayBatDau = person.Hired, NgayKetThuc = fixedTerm ? person.Hired.AddYears(1).AddDays(-1) : null,
                ThoiHan = fixedTerm ? 12 : null, IsActive = true
            });

            var salary = new ThongTinLuongNV
            {
                NhanVien = employee, LuongCoBan = person.Pay, NgayApDung = payrollStart.ToDateTime(TimeOnly.MinValue),
                BHXH = person.Pay * .08m, BHYT = person.Pay * .015m, BHTN = person.Pay * .01m, IsActive = true
            };
            db.ThongTinLuongNVs.Add(salary);
            staff.Add((employee, salary, []));
            var years = DataThrough.Year - person.Hired.Year - (person.Hired.AddYears(DataThrough.Year - person.Hired.Year) > DataThrough ? 1 : 0);
            var entitlement = fixedTerm ? 12 * (12 - person.Hired.Month + 1) / 12 : 12 + years / 5;
            var used = leaveDates.Count(x => x.Index == index && x.Status == LeaveStatus.Approved);
            db.SoDuPheps.Add(new SoDuPhep
            {
                MaNvNavigation = employee, Nam = DataThrough.Year,
                NgayCapNhat = DataThrough.ToDateTime(ShiftEnd), SoNgayConLai = entitlement - used
            });
            db.PhepNamNhanViens.Add(new PhepNamNhanVien
            {
                MaNvNavigation = employee, CauHinhPhepNam = leavePolicy, Nam = DataThrough.Year,
                NgayCapNhat = DataThrough.ToDateTime(ShiftEnd), SoNgayPhepDuocCap = entitlement,
                SoNgayDaSuDung = used, SoNgayChuaSuDung = entitlement - used
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
        var director = staff[0].Employee;
        var sampleEmployee = staff[4].Employee;
        db.WorksiteAssignments.Add(new WorksiteAssignment
        {
            Employee = staff[2].Employee, Worksite = canGio, EffectiveFrom = new DateOnly(2026, 10, 1)
        });
        for (var index = 0; index < staff.Count; index++)
        {
            if (DemoRoster.Team[index].Manager is int manager)
                db.EmployeeReportingLines.Add(new EmployeeReportingLine
                {
                    Employee = staff[index].Employee, Manager = staff[manager].Employee
                });
        }
        for (var index = 0; index < leaveDates.Length; index++)
        {
            var leave = leaveDates[index];
            AddLeave(db, staff[leave.Index].Employee, leaveType, director, leave.Date, $"NP{index + 1:000}", leave.Status, leave.Reason);
        }
        var approvedLeave = leaveDates.Where(x => x.Status == LeaveStatus.Approved)
            .Select(x => (staff[x.Index].Employee.MaNv, x.Date)).ToHashSet();
        var pendingLeave = leaveDates.Where(x => x.Status == LeaveStatus.Pending)
            .Select(x => (staff[x.Index].Employee.MaNv, x.Date)).ToHashSet();
        db.LoaiNgayNghis.AddRange(
            new LoaiNgayNghi { TenLoai = "Nghỉ việc riêng hưởng lương", HuongLuong = true },
            new LoaiNgayNghi { TenLoai = "Nghỉ không hưởng lương", HuongLuong = false });

        for (var date = StartOfYear; date <= DataThrough; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            foreach (var account in staff)
            {
                if (date < account.Employee.NgayVaoLam || pendingLeave.Contains((account.Employee.MaNv, date)))
                    continue;

                var paidLeave = approvedLeave.Contains((account.Employee.MaNv, date));
                var holiday = holidayDays.Contains(date);
                var shiftStart = account.Employee.MaNv % 3 == 0 ? new TimeOnly(8, 15) : ShiftStart;
                var entry = new ChamCong
                {
                    MaNv = account.Employee.MaNv, NgayLamViec = date,
                    GioVao = paidLeave || holiday ? null : shiftStart, GioRa = paidLeave || holiday ? null : shiftStart.AddHours(9),
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
        foreach (var index in new[] { 8, 10, 11, 14, 15, 16, 18, 21, 22 })
        {
            var employee = staff[index].Employee;
            var start = employee.MaNv % 3 == 0 ? new TimeOnly(17, 15) : ShiftEnd;
            db.TangCas.Add(new TangCa
            {
                MaNv = employee.MaNv, NgayTangCa = new DateOnly(2026, 10, 22),
                GioVao = start, GioRa = start.AddHours(1), SoGioTangCa = 1, TyLeTangCa = 1.5m,
                TrangThai = OvertimeStatus.Approved, MaNvDuyet = director.MaNv, GhiChu = "Xử lý lô giao hàng cuối ngày"
            });
        }
        var courses = new[]
        {
            (Department: "Kho vận", Title: "An toàn kho và quy trình giao nhận", Content: "Kiểm tra kiện hàng, phân vùng nâng hạ và bàn giao phiếu xuất."),
            (Department: "Điều phối vận tải", Title: "Điều phối tuyến và xử lý sự cố", Content: "Xác nhận lịch xe, thông báo giao trễ và xử lý biên bản tại điểm giao."),
            (Department: "Tài chính – Kế toán", Title: "Đối chiếu cước vận tải", Content: "Kiểm tra phụ phí, chứng từ và công nợ theo chuyến.")
        };
        foreach (var course in courses)
        {
            var training = new DaoTao
            {
                PhongBan = departments[course.Department], TenDaoTao = course.Title, NoiDung = course.Content,
                NgayBatDau = new DateOnly(2026, 10, 12), NgayKetThuc = new DateOnly(2026, 11, 30)
            };
            db.DaoTaos.Add(training);
            foreach (var member in staff.Where(x => x.Employee.MaPhongBan == training.PhongBan.MaPhongBan))
                db.LichSuDaoTaos.Add(new LichSuDaoTao
                {
                    MaDaoTaoNavigation = training, MaNvNavigation = member.Employee,
                    KetQua = member.Employee.MaNv % 3 == 0 ? "Chưa Hoàn Thành" : "Hoàn Thành"
                });
        }
        await db.SaveChangesAsync();

        var payrollCount = 0;
        for (var month = StartOfYear; month.Month < DataThrough.Month; month = month.AddMonths(1))
        {
            for (var index = 0; index < staff.Count; index++)
            {
                var member = staff[index];
                var attendance = member.Attendance.Where(x => x.NgayLamViec.Month == month.Month).ToList();
                if (attendance.Count == 0)
                    continue;

                var pay = await calculator.CalculatePayroll(member.Employee.MaNv, month.ToDateTime(TimeOnly.MinValue),
                    attendance, member.Salary);
                pay.NgayTao = month.AddMonths(1).ToDateTime(new TimeOnly(9, 0));
                pay.NguoiTao = "demo.payroll";
                pay.TrangThai = month.Month == 10 ? index switch
                {
                    1 or 5 => PayrollStatus.SentToAccounting,
                    2 or 6 => PayrollStatus.AccountingApproved,
                    3 => PayrollStatus.DirectorApproved,
                    10 or 16 => PayrollStatus.EmployeeConfirmed,
                    11 => PayrollStatus.EmployeeRejected,
                    15 => PayrollStatus.Created,
                    21 => PayrollStatus.AccountingRejected,
                    _ => PayrollStatus.SentToEmployee
                } : PayrollStatus.SentToEmployee;
                db.BangLuongs.Add(pay);
                payrollCount++;
            }
        }

        await db.SaveChangesAsync();
        await seedTransaction.CommitAsync();
        logger.LogInformation("Demo data seeded through {Cutoff:yyyy-MM-dd}: {Employees} employees, {Sites} worksites, {Attendance} attendance entries, {Payrolls} payrolls, {Leave} leave entries.",
            DataThrough, staff.Count, sites.Count, staff.Sum(x => x.Attendance.Count), payrollCount, leaveDates.Length);
    }

    private static void AddLeave(HrDbContext db, NhanVien employee, LoaiNgayNghi leaveType,
        NhanVien director, DateOnly date, string code, string status, string reason)
    {
        var submitted = date.AddDays(-3).ToDateTime(new TimeOnly(9, 0));
        db.NgayNghis.Add(new NgayNghi
        {
            MaNvNavigation = employee, MaLoaiNgayNghiNavigation = leaveType, NgayNghi1 = date,
            NgayLamDon = submitted, NgayDuyet = submitted,
            NguoiDuyetId = status == LeaveStatus.Pending ? null : director.MaNv,
            LyDoTuChoi = status == LeaveStatus.Rejected ? "Đã chốt nhân sự trực điều phối" : null,
            MaTrangThai = status, MaDon = code, LyDo = reason
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

    private static async Task EnsureDemoExtensionsAsync(HrDbContext db)
    {
        // Demo databases are created with EnsureCreated and have no EF migration history.
        // Add only the new tables so a local SQLite demo keeps its existing records.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS EmployeeReportingLines (
                EmployeeId INTEGER NOT NULL PRIMARY KEY REFERENCES NhanVien (MaNV) ON DELETE RESTRICT,
                ManagerId INTEGER NOT NULL REFERENCES NhanVien (MaNV) ON DELETE RESTRICT,
                CONSTRAINT CK_EmployeeReportingLines_DifferentEmployees CHECK (EmployeeId <> ManagerId)
            );
            CREATE INDEX IF NOT EXISTS IX_EmployeeReportingLines_ManagerId ON EmployeeReportingLines (ManagerId);

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
