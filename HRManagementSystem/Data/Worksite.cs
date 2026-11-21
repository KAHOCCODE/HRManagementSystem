using System.ComponentModel.DataAnnotations;

namespace HR_KD.Data;

public sealed class Worksite
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Mã chi nhánh")]
    public string Code { get; set; } = "";

    [Required, StringLength(120)]
    [Display(Name = "Tên chi nhánh")]
    public string Name { get; set; } = "";

    [Required, StringLength(80)]
    [Display(Name = "Tỉnh hoặc thành phố")]
    public string Province { get; set; } = "TP.HCM";

    [Required, StringLength(80)]
    [Display(Name = "Xã, phường hoặc đặc khu")]
    public string Ward { get; set; } = "";

    [Required, StringLength(255)]
    [Display(Name = "Số nhà, đường hoặc địa chỉ cụ thể")]
    public string Address { get; set; } = "";

    [Range(1, 4)]
    [Display(Name = "Vùng lương năm 2026")]
    public int? Region2026 { get; set; }

    [Range(1, 4)]
    [Display(Name = "Vùng lương năm 2027")]
    public int? Region2027 { get; set; }
}

public sealed class WorksiteAssignment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public NhanVien Employee { get; set; } = null!;
    public int WorksiteId { get; set; }
    public Worksite Worksite { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
}
