namespace HR_KD.Data;

internal static class DemoRoster
{
    internal const string CompanyName = "Công ty TNHH Logistics An Phú Nam";

    internal sealed record Person(string Name, bool Male, int BirthYear, string Position, string Department,
        string Site, int? Manager, decimal Pay, DateOnly Hired, string? Login = null, string? Role = null,
        int Dependents = 0);

    internal static readonly Person[] Team =
    [
        new("Nguyễn Minh An", true, 1971, "Giám đốc", "Ban giám đốc", "SG", null, 36000000, new(2018, 4, 2), "demo.director", "DIRECTOR", 2),
        new("Trần Khánh Linh", false, 1990, "Trưởng phòng nhân sự", "Nhân sự – Hành chính", "SG", 0, 24000000, new(2019, 6, 3), "demo.hr", "EMPLOYEE_MANAGER", 1),
        new("Lê Hoàng Nam", true, 1987, "Quản lý chi nhánh", "Kho vận", "SG", 0, 21000000, new(2020, 3, 2), "demo.manager", "LINE_MANAGER", 2),
        new("Phạm Gia Hân", false, 1991, "Kế toán tiền lương", "Tài chính – Kế toán", "SG", 7, 16000000, new(2022, 9, 12), "demo.payroll", "PAYROLL_AUDITOR"),
        new("Võ Thanh Bình", true, 2001, "Nhân viên kho", "Kho vận", "CG", 2, 12000000, new(2025, 1, 6), "demo.employee", "EMPLOYEE"),
        new("Đỗ Ngọc Mai", false, 1996, "Chuyên viên tuyển dụng", "Nhân sự – Hành chính", "SG", 1, 13600000, new(2023, 2, 13)),
        new("Trương Quốc Vinh", true, 1980, "Trưởng phòng vận tải", "Điều phối vận tải", "TD", 0, 23900000, new(2019, 8, 5), Dependents: 2),
        new("Bùi Mỹ Duyên", false, 1989, "Kế toán trưởng", "Tài chính – Kế toán", "SG", 0, 26200000, new(2019, 10, 7), Dependents: 1),
        new("Vũ Quốc Toàn", true, 1994, "Điều phối viên", "Điều phối vận tải", "TD", 6, 14500000, new(2024, 8, 5)),
        new("Lâm Thu Hương", false, 1986, "Trưởng phòng kinh doanh", "Kinh doanh – Khách hàng", "SG", 0, 24800000, new(2020, 5, 4), Dependents: 1),
        new("Hồ Văn Điệp", true, 1974, "Tổ trưởng kho", "Kho vận", "CG", 2, 16800000, new(2021, 9, 6), Dependents: 1),
        new("Ngô Hoài An", false, 1999, "Nhân viên chứng từ", "Kho vận", "CG", 10, 10900000, new(2026, 3, 1)),
        new("Phan Tiến Đạt", true, 1996, "Chuyên viên kinh doanh", "Kinh doanh – Khách hàng", "SG", 9, 14600000, new(2024, 1, 15)),
        new("Đặng Thùy Trang", false, 1988, "Chuyên viên khách hàng", "Kinh doanh – Khách hàng", "SG", 9, 13900000, new(2021, 4, 12), Dependents: 1),
        new("Nguyễn Văn Sơn", true, 1978, "Tổ trưởng kho", "Kho vận", "TD", 0, 17800000, new(2020, 7, 6), Dependents: 2),
        new("Lý Hồng Ngân", false, 2002, "Nhân viên kho", "Kho vận", "TD", 14, 9800000, new(2026, 6, 1)),
        new("Trịnh Anh Khoa", true, 1999, "Nhân viên kho", "Kho vận", "CG", 10, 10400000, new(2025, 2, 10)),
        new("Dương Bảo Trâm", false, 1993, "Kế toán công nợ", "Tài chính – Kế toán", "SG", 7, 14800000, new(2023, 6, 5), Dependents: 1),
        new("Lê Văn Lộc", true, 1975, "Tài xế giao nhận", "Điều phối vận tải", "TD", 6, 13400000, new(2022, 6, 6), Dependents: 2),
        new("Châu Yến Nhi", false, 2001, "Điều phối viên", "Điều phối vận tải", "CG", 6, 11800000, new(2026, 7, 1)),
        new("Đinh Hữu Phước", true, 1988, "Chuyên viên hệ thống", "Công nghệ – Hệ thống", "SG", 0, 21400000, new(2021, 11, 15), Dependents: 1),
        new("Huỳnh Minh Thư", false, 1997, "Nhân viên kho", "Kho vận", "CG", 10, 10700000, new(2025, 5, 12)),
        new("Mai Thành Luân", true, 1992, "Tài xế giao nhận", "Điều phối vận tải", "TD", 6, 12700000, new(2026, 5, 1), Dependents: 1),
        new("Tạ Cẩm Tú", false, 1998, "Chuyên viên hệ thống", "Công nghệ – Hệ thống", "SG", 20, 15100000, new(2026, 9, 1))
    ];
}
