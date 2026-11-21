using HR_KD.Data;
using ClosedXML.Excel;
using HR_KD.DTOs;
using OfficeOpenXml;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace HR_KD.Services
{
    public class ExcelTemplateService
    {
        private readonly HrDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ExcelTemplateService(HrDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        #region Tạo file excel mẫu
        public byte[] GenerateTemplateWithData()
        {
            using var workbook = new XLWorkbook();

            // Sheet nhập dữ liệu
            var sheet = workbook.Worksheets.Add("NhanVienImport");

            var headers = new[]
            {
                "HoTen", "NgaySinh (dd/MM/yyyy)", "GioiTinh (Nam/Nu)", "SDT",
                "Email", "TrinhDoHocVan", "MaChucVu", "MaPhongBan", "NgayVaoLam (dd/MM/yyyy)", "MaChiNhanh", "MaQuanLyTrucTiep"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.SkyBlue;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Cài format cột
            sheet.Column("B").Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Column("I").Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Column("D").Style.NumberFormat.Format = "@";

            // Auto-fit column width
            sheet.Columns().AdjustToContents();

            // Sheet tham chiếu
            var refSheet = workbook.Worksheets.Add("DanhMucThamChieu");
            refSheet.Cell("A1").Value = "MaChucVu";
            refSheet.Cell("B1").Value = "TenChucVu";
            refSheet.Cell("D1").Value = "MaPhongBan";
            refSheet.Cell("E1").Value = "TenPhongBan";

            var chucVus = _context.ChucVus.ToList();
            var phongBans = _context.PhongBans.ToList();

            for (int i = 0; i < chucVus.Count; i++)
            {
                refSheet.Cell(i + 2, 1).Value = chucVus[i].MaChucVu;
                refSheet.Cell(i + 2, 2).Value = chucVus[i].TenChucVu;
            }

            for (int i = 0; i < phongBans.Count; i++)
            {
                refSheet.Cell(i + 2, 4).Value = phongBans[i].MaPhongBan;
                refSheet.Cell(i + 2, 5).Value = phongBans[i].TenPhongBan;
            }

            // Tạo dropdown MaChucVu
            var chucVuRange = refSheet.Range($"A2:A{chucVus.Count + 1}");
            sheet.Range("G2:G100").CreateDataValidation().List(chucVuRange);

            // Tạo dropdown MaPhongBan
            var phongBanRange = refSheet.Range($"D2:D{phongBans.Count + 1}");
            sheet.Range("H2:H100").CreateDataValidation().List(phongBanRange);

            refSheet.Cell("G1").Value = "MaChiNhanh";
            refSheet.Cell("H1").Value = "Chi nhánh";
            refSheet.Cell("J1").Value = "MaQuanLyTrucTiep";
            refSheet.Cell("K1").Value = "Họ tên quản lý";
            var worksites = _context.Worksites.OrderBy(x => x.Id).ToList();
            var managers = _context.NhanViens.OrderBy(x => x.MaNv)
                .Select(x => new { x.MaNv, x.HoTen }).ToList();
            for (var i = 0; i < worksites.Count; i++)
            {
                refSheet.Cell(i + 2, 7).Value = worksites[i].Id;
                refSheet.Cell(i + 2, 8).Value = worksites[i].Name;
            }
            for (var i = 0; i < managers.Count; i++)
            {
                refSheet.Cell(i + 2, 10).Value = managers[i].MaNv;
                refSheet.Cell(i + 2, 11).Value = managers[i].HoTen;
            }
            refSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        #endregion


        public async Task<ExcelImportResult> ParseExcelWithValidation(Stream stream)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var result = new ExcelImportResult();
            var employees = new List<ImportNhanVienDto>();
            var errors = new Dictionary<int, string>();

            using var package = new ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets.FirstOrDefault(x => x.Name == "NhanVienImport");
            if (sheet == null)
            {
                result.Errors[0] = "Không tìm thấy sheet 'NhanVienImport'. Vui lòng dùng đúng file mẫu.";
                return result;
            }
            int lastRow = sheet.Dimension?.End.Row ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                if (Enumerable.Range(1, 11).All(column => string.IsNullOrWhiteSpace(sheet.Cells[row, column].Text)))
                    continue;

                var employee = new ImportNhanVienDto
                {
                    RowIndex = row,
                    HoTen = sheet.Cells[row, 1].Text.Trim(),
                    NgaySinh = ReadDate(sheet.Cells[row, 2]),
                    GioiTinh = string.Equals(sheet.Cells[row, 3].Text.Trim(), "Nam", StringComparison.OrdinalIgnoreCase),
                    Sdt = sheet.Cells[row, 4].Text.Trim(),
                    Email = sheet.Cells[row, 5].Text.Trim(),
                    TrinhDoHocVan = sheet.Cells[row, 6].Text.Trim(),
                    MaChucVu = ReadId(sheet.Cells[row, 7]),
                    MaPhongBan = ReadId(sheet.Cells[row, 8]),
                    NgayVaoLam = ReadDate(sheet.Cells[row, 9]),
                    WorksiteId = ReadId(sheet.Cells[row, 10]),
                    ManagerId = ReadId(sheet.Cells[row, 11])
                };
                employee.RowError = await ValidateAsync(employee);
                if (employee.RowError != null)
                    errors[row] = employee.RowError;
                employees.Add(employee);
            }

            result.Employees = employees;
            result.Errors = errors;
            return result;
        }

        public async Task<string?> ValidateAsync(ImportNhanVienDto employee)
        {
            var errors = new List<string>();
            employee.HoTen = employee.HoTen?.Trim() ?? "";
            employee.Email = employee.Email?.Trim() ?? "";
            employee.Sdt = employee.Sdt?.Trim() ?? "";
            if (employee.HoTen.Length is 0 or > 100)
                errors.Add("Họ tên phải có từ 1 đến 100 ký tự.");
            if (employee.NgaySinh == default)
                errors.Add("Ngày sinh không hợp lệ. Dùng định dạng dd/MM/yyyy.");
            if (employee.NgayVaoLam == default)
                errors.Add("Ngày vào làm không hợp lệ. Dùng định dạng dd/MM/yyyy.");
            if (employee.NgaySinh != default && employee.NgayVaoLam != default && employee.NgaySinh.Date >= employee.NgayVaoLam.Date)
                errors.Add("Ngày vào làm phải sau ngày sinh.");

            if (employee.Email.Length is 0 or > 100 || !new EmailAddressAttribute().IsValid(employee.Email))
                errors.Add("Email không hợp lệ.");
            else if (await _context.NhanViens.AnyAsync(x => x.Email != null && x.Email.ToLower() == employee.Email.ToLower()))
                errors.Add("Email đã tồn tại.");

            if (!Regex.IsMatch(employee.Sdt, @"^(\+84|0)[1-9]\d{8,9}$"))
                errors.Add("Số điện thoại không hợp lệ.");
            else if (await _context.NhanViens.AnyAsync(x => x.Sdt == employee.Sdt))
                errors.Add("Số điện thoại đã tồn tại.");

            if (!await _context.ChucVus.AnyAsync(x => x.MaChucVu == employee.MaChucVu))
                errors.Add("Chọn chức vụ có trong danh mục.");
            if (!await _context.PhongBans.AnyAsync(x => x.MaPhongBan == employee.MaPhongBan))
                errors.Add("Chọn phòng ban có trong danh mục.");
            if (employee.WorksiteId is int site && !await _context.Worksites.AnyAsync(x => x.Id == site))
                errors.Add("Mã chi nhánh không có trong danh mục.");
            if (employee.ManagerId is int manager && !await _context.NhanViens.AnyAsync(x => x.MaNv == manager))
                errors.Add("Mã quản lý trực tiếp không có trong danh mục.");

            return errors.Count == 0 ? null : string.Join(" ", errors);
        }

        private static int? ReadId(ExcelRange cell) =>
            string.IsNullOrWhiteSpace(cell.Text) ? null
                : int.TryParse(cell.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0;

        private static DateTime ReadDate(ExcelRange cell)
        {
            if (cell.Value is DateTime date)
                return date.Date;
            if (cell.Value is double serial && serial >= 1 && serial < 2958466 && serial != 60)
                return DateTime.FromOADate(serial < 60 ? serial + 1 : serial).Date;
            return DateTime.TryParseExact(cell.Text.Trim(), ["d/M/yyyy", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date : default;
        }
    }
}
