using System.ComponentModel.DataAnnotations;

namespace HR_KD.Models;

public sealed class EmployeeProfile
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nhập họ tên."), StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string Name { get; set; } = "";

    [EmailAddress, StringLength(100)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Phone, StringLength(20)]
    [Display(Name = "Điện thoại")]
    public string? Phone { get; set; }

    [StringLength(255)]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Chọn phòng ban.")]
    [Display(Name = "Phòng ban")]
    public int DepartmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Chọn chức vụ.")]
    [Display(Name = "Chức vụ")]
    public int PositionId { get; set; }

    [Display(Name = "Quản lý trực tiếp")]
    public int? ManagerId { get; set; }

    [Display(Name = "Ảnh nhân viên")]
    public IFormFile? Photo { get; set; }

    public string? AvatarUrl { get; set; }
}

public sealed class OrganizationMember
{
    public int Id { get; init; }
    public int? ManagerId { get; set; }
    public string Name { get; init; } = "";
    public string Position { get; init; } = "";
    public string Department { get; init; } = "";
    public int? WorksiteId { get; init; }
    public string? Worksite { get; set; }
    public string? AvatarUrl { get; init; }
    public List<OrganizationMember> Reports { get; } = [];
}

public sealed record OrganizationWorksite(int Id, string Name);
public sealed record OrganizationChartPage(string Name, int EmployeeCount, IReadOnlyList<OrganizationMember> Roots,
    IReadOnlyList<OrganizationWorksite> Worksites, int? SelectedWorksiteId);
