using HR_KD.Data;

namespace HR_KD.Authorization;

internal static class ReportingAccess
{
    public static IQueryable<int> DirectReports(HrDbContext db, int? managerId) =>
        db.NhanViens.Where(employee =>
            db.EmployeeReportingLines.Any(line => line.EmployeeId == employee.MaNv && line.ManagerId == managerId)
            || (!db.EmployeeReportingLines.Any(line => line.EmployeeId == employee.MaNv)
                && db.NhanViens.Any(manager => manager.MaNv == managerId
                    && manager.MaPhongBan == employee.MaPhongBan && manager.MaChucVu < employee.MaChucVu)))
            .Select(employee => employee.MaNv);
}
