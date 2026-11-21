using System.Security.Claims;
using HR_KD.Data;

namespace HR_KD.Authorization;

internal static class PayrollAccess
{
    internal const string ReviewRoles = "DIRECTOR,PAYROLL_AUDITOR,EMPLOYEE_MANAGER,LINE_MANAGER";
    internal const string CompanyRoles = "DIRECTOR,PAYROLL_AUDITOR";
    internal const string LineManager = "LINE_MANAGER";

    internal static int? EmployeeId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("MaNV"), out var id) ? id : null;

    internal static IQueryable<BangLuong> DepartmentPayrolls(HrDbContext db, ClaimsPrincipal user, bool forUpdate = false)
    {
        if (!forUpdate && (user.IsInRole("DIRECTOR") || user.IsInRole("PAYROLL_AUDITOR")))
            return db.BangLuongs;

        var employeeId = EmployeeId(user);
        if (employeeId == null || (!user.IsInRole(LineManager) && (forUpdate || !user.IsInRole("EMPLOYEE_MANAGER"))))
            return db.BangLuongs.Where(_ => false);

        return db.BangLuongs.Where(payroll => db.NhanViens.Any(manager =>
            manager.MaNv == employeeId && manager.MaPhongBan == payroll.MaNvNavigation.MaPhongBan
            && manager.MaChucVu < payroll.MaNvNavigation.MaChucVu));
    }

    internal static IQueryable<BangLuong> VisiblePayrolls(HrDbContext db, ClaimsPrincipal user)
    {
        var employeeId = EmployeeId(user);
        var department = DepartmentPayrolls(db, user);
        return db.BangLuongs.Where(payroll => payroll.MaNv == employeeId
            || department.Any(visible => visible.MaLuong == payroll.MaLuong));
    }
}
