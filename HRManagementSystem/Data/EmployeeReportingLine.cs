namespace HR_KD.Data;

public sealed class EmployeeReportingLine
{
    public int EmployeeId { get; set; }
    public NhanVien Employee { get; set; } = null!;
    public int ManagerId { get; set; }
    public NhanVien Manager { get; set; } = null!;
}
