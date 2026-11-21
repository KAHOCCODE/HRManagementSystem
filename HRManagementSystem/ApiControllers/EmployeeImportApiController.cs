using Microsoft.AspNetCore.Authorization;
using HR_KD.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR_KD.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "EMPLOYEE_MANAGER,DIRECTOR")]
    public class EmployeeImportApiController : ControllerBase
    {
        private readonly ExcelTemplateService _excelService;

        public EmployeeImportApiController(ExcelTemplateService excelService)
        {
            _excelService = excelService;
        }

        [HttpPost("ImportExcel")]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Không có file nào được chọn." });

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _excelService.ParseExcelWithValidation(stream);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi xử lý file Excel.", detail = ex.Message });
            }
        }
    }
}
