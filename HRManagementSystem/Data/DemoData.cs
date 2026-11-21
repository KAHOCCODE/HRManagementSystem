using System.Security.Cryptography;
using System.Text;
using HR_KD.Common;
using Microsoft.EntityFrameworkCore;

namespace HR_KD.Data;

internal static class DemoData
{
    internal static async Task InitializeAsync(HrDbContext db, string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16 || Encoding.UTF8.GetByteCount(password) > 48)
        {
            throw new InvalidOperationException("Demo:Password must be at least 16 characters and at most 48 UTF-8 bytes.");
        }

        if (!await db.Database.EnsureCreatedAsync())
            await AddWorksiteTablesToExistingDemoAsync(db);
        if (await db.TaiKhoans.AnyAsync())
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var headquarters = new Worksite
        {
            Code = "HCM-SAIGON", Name = "Văn phòng mẫu Sài Gòn",
            Ward = "Phường Sài Gòn", Address = "Địa chỉ giả lập", Region2026 = 1, Region2027 = 1
        };
        var canGio = new Worksite
        {
            Code = "HCM-CANGIO", Name = "Chi nhánh mẫu Cần Giờ",
            Ward = "Xã Cần Giờ", Address = "Địa chỉ giả lập", Region2026 = 2, Region2027 = 1
        };
        var department = new PhongBan { TenPhongBan = "Vận hành", MoTa = "Phòng ban minh họa" };
        var leaveType = new LoaiNgayNghi
        {
            TenLoai = "Nghỉ phép năm",
            HuongLuong = true,
            TinhVaoPhepNam = true
        };
        var leavePolicy = new CauHinhPhepNam { Nam = today.Year, SoNgayPhepMacDinh = 12 };
        var sampleUsers = new[]
        {
            (Username: "demo.director", Name: "Nguyễn Minh An", Role: "DIRECTOR", Title: "Giám đốc"),
            (Username: "demo.hr", Name: "Trần Khánh Linh", Role: "EMPLOYEE_MANAGER", Title: "Quản lý nhân sự"),
            (Username: "demo.manager", Name: "Lê Hoàng Nam", Role: "LINE_MANAGER", Title: "Trưởng phòng"),
            (Username: "demo.payroll", Name: "Phạm Gia Hân", Role: "PAYROLL_AUDITOR", Title: "Kế toán lương"),
            (Username: "demo.employee", Name: "Võ Thanh Bình", Role: "EMPLOYEE", Title: "Nhân viên")
        };
        foreach (var sample in sampleUsers)
        {
            var employee = new NhanVien
            {
                HoTen = sample.Name,
                Email = sample.Username + "@example.invalid",
                NgayVaoLam = today.AddYears(-1),
                MaPhongBanNavigation = department,
                MaChucVuNavigation = new ChucVu { TenChucVu = sample.Title }
            };
            db.WorksiteAssignments.Add(new WorksiteAssignment
            {
                Employee = employee,
                Worksite = sample.Role == "EMPLOYEE" ? canGio : headquarters,
                EffectiveFrom = new DateOnly(2026, 1, 1)
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
                    MoTaQuyenHan = "Vai trò minh họa"
                }
            });
            db.TaiKhoans.Add(account);

            if (sample.Role == "EMPLOYEE")
            {
                db.ChamCongs.Add(new ChamCong
                {
                    MaNvNavigation = employee,
                    NgayLamViec = today,
                    GioVao = new TimeOnly(8, 0),
                    GioRa = new TimeOnly(17, 0),
                    TongGio = 8,
                    TrangThai = AttendanceStatus.Approved
                });
                db.NgayNghis.Add(new NgayNghi
                {
                    MaNvNavigation = employee,
                    MaLoaiNgayNghiNavigation = leaveType,
                    NgayNghi1 = today.AddDays(7),
                    NgayLamDon = DateTime.Now,
                    MaTrangThai = LeaveStatus.Pending,
                    MaDon = "DEMO-001",
                    LyDo = "Đơn nghỉ phép minh họa"
                });
                db.PhepNamNhanViens.Add(new PhepNamNhanVien
                {
                    MaNvNavigation = employee,
                    CauHinhPhepNam = leavePolicy,
                    Nam = today.Year,
                    SoNgayPhepDuocCap = 12,
                    SoNgayChuaSuDung = 12
                });
                db.BangLuongs.Add(new BangLuong
                {
                    MaNvNavigation = employee,
                    ThangNam = new DateOnly(today.Year, today.Month, 1),
                    NgayTao = DateTime.Now,
                    TongLuong = 12000000,
                    ThucNhan = 10800000,
                    TrangThai = PayrollStatus.SentToEmployee,
                    GhiChu = "Bảng lương minh họa"
                });
            }
        }

        db.ChamCongGioRaVaos.Add(new ChamCongGioRaVao
        {
            GioVao = new TimeOnly(8, 0), GioRa = new TimeOnly(17, 0), TongGio = 8, KichHoat = true
        });
        db.LichLamViecs.AddRange(
            new LichLamViec { TenLich = "T2 - T6", Nam = today.Year, KichHoat = true },
            new LichLamViec { TenLich = "T2 - Nửa T7", Nam = today.Year },
            new LichLamViec { TenLich = "T2 - T7", Nam = today.Year });
        db.TiLeTangCas.AddRange(
            new TiLeTangCa { TenTiLeTangCa = "Ngày thường", TiLe = 150, KichHoat = true },
            new TiLeTangCa { TenTiLeTangCa = "Ngày nghỉ hằng tuần", TiLe = 200, KichHoat = true },
            new TiLeTangCa { TenTiLeTangCa = "Ngày lễ", TiLe = 300, KichHoat = true });

        db.TrangThais.AddRange(
            new TrangThai { MaTrangThai = AttendanceStatus.Pending, TenTrangThai = "Chờ duyệt" },
            new TrangThai { MaTrangThai = AttendanceStatus.Approved, TenTrangThai = "Đã duyệt" },
            new TrangThai { MaTrangThai = PayrollStatus.SentToEmployee, TenTrangThai = "Đã gửi nhân viên" },
            new TrangThai { MaTrangThai = LeaveStatus.Pending, TenTrangThai = "Chờ duyệt" },
            new TrangThai { MaTrangThai = LeaveStatus.Approved, TenTrangThai = "Đã duyệt" },
            new TrangThai { MaTrangThai = LeaveStatus.Rejected, TenTrangThai = "Đã từ chối" });
        await db.SaveChangesAsync();
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
