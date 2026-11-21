using HR_KD.Authorization;
using HR_KD.Data;
using HR_KD.Helpers;
using System.Security.Claims;
using HR_KD.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var demoMode = builder.Configuration.GetValue<bool>("Demo:Enabled");
var httpsAtProxy = builder.Configuration.GetValue<bool>("Hosting:HttpsAtProxy");
var cookieSecurity = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

// Thêm hỗ trợ IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Thêm Session vào hệ thống
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // 30 phút hết hạn
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = cookieSecurity;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IAuthorizationHandler, ManageSubordinateHandler>();
builder.Services.AddScoped<ExcelTemplateService>();
builder.Services.AddScoped<UsernameGeneratorService>();
builder.Services.AddScoped<PayrollCalculator>();
builder.Services.AddScoped<PayrollReportService>();

// Cấu hình Authentication 
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.SecurePolicy = cookieSecurity;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Events.OnValidatePrincipal = async context =>
        {
            var principal = context.Principal;
            var username = principal?.Identity?.Name;
            var db = context.HttpContext.RequestServices.GetRequiredService<HrDbContext>();
            var account = await db.TaiKhoans.AsNoTracking().Include(x => x.TaiKhoanQuyenHans)
                .SingleOrDefaultAsync(x => x.Username == username, context.HttpContext.RequestAborted);
            if (account == null || principal == null
                || principal.FindFirstValue("hr:session") != PasswordHelper.SessionStamp(account.PasswordHash)
                || !account.TaiKhoanQuyenHans.Select(x => x.MaQuyenHan).ToHashSet()
                    .SetEquals(principal.FindAll(ClaimTypes.Role).Select(x => x.Value)))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(context.Scheme.Name);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("EMPLOYEE", policy => policy.RequireRole("EMPLOYEE"));
    options.AddPolicy("EMPLOYEE_MANAGER", policy => policy.RequireRole("EMPLOYEE_MANAGER"));
    options.AddPolicy("LINE_MANAGER", policy => policy.RequireRole("LINE_MANAGER"));
    options.AddPolicy("DIRECTOR", policy => policy.RequireRole("DIRECTOR"));

    options.AddPolicy("EmployeeView", policy => policy.Requirements.Add(new ManageSubordinateRequirement("employees", "view")));
    options.AddPolicy("EmployeeCreate", policy => policy.Requirements.Add(new ManageSubordinateRequirement("employees", "create")));
    options.AddPolicy("RoleManage", policy => policy.Requirements.Add(new ManageSubordinateRequirement("employees", "assign_roles")));
    options.AddPolicy("AttendanceManage", policy => policy.Requirements.Add(new ManageSubordinateRequirement("attendance", "manage")));
    options.AddPolicy("HolidayCreate", policy => policy.Requirements.Add(new ManageSubordinateRequirement("holidays", "create")));
    options.AddPolicy("HolidayApprove", policy => policy.Requirements.Add(new ManageSubordinateRequirement("holidays", "approve")));
    options.AddPolicy("PayrollCreate", policy => policy.Requirements.Add(new ManageSubordinateRequirement("payroll", "create")));
    options.AddPolicy("PayrollApproveBL2", policy => policy.Requirements.Add(new ManageSubordinateRequirement("payroll", "approve_bl2_to_bl3")));
    options.AddPolicy("PayrollApproveBL3", policy => policy.Requirements.Add(new ManageSubordinateRequirement("payroll", "approve_bl3_to_bl4")));


});

// Cấu hình Entity Framework
if (demoMode)
{
    var databasePath = builder.Configuration["Demo:DatabasePath"]
        ?? Path.Combine(Path.GetTempPath(), "hr-demo", "hr.db");
    databasePath = Path.GetFullPath(databasePath);
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    var demoConnection = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    builder.Services.AddDbContext<HrDbContext>(options => options.UseSqlite(demoConnection));
}
else
{
    builder.Services.AddDbContext<HrDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("Db_HR")));
}
builder.Services.AddScoped<PhepNamService>(); // Đăng ký dịch vụ với vòng đời Scoped

if (!demoMode)
{
    builder.Services.AddHostedService<YearlyTaskService>();
}

//// Đăng ký HttpClient và HolidayNotificationService
//builder.Services.AddHttpClient();
//builder.Services.AddHostedService<HolidayNotificationService>();

// Thêm Controllers và Views
builder.Services.AddControllersWithViews();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

if (demoMode)
{
    await using var scope = app.Services.CreateAsyncScope();
    await DemoData.InitializeAsync(
        scope.ServiceProvider.GetRequiredService<HrDbContext>(),
        scope.ServiceProvider.GetRequiredService<PayrollCalculator>(),
        app.Configuration["Demo:Password"], app.Logger);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!httpsAtProxy)
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Uploads") && context.GetEndpoint() == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    if (context.Request.Path.StartsWithSegments("/avatars") && context.User.Identity?.IsAuthenticated != true)
    {
        await context.ChallengeAsync();
        return;
    }
    await next();
});
app.UseStaticFiles();

app.UseSession(); 

app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok()).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
