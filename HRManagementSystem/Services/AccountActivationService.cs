using System.Net;
using System.Security.Cryptography;
using HR_KD.Helpers;
using Microsoft.AspNetCore.DataProtection;

namespace HR_KD.Services;

public sealed class AccountActivationService(
    IDataProtectionProvider dataProtection,
    IConfiguration configuration,
    EmailService emailService)
{
    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("HR.AccountActivation.v1").ToTimeLimitedDataProtector();

    public bool TryRead(string? ticket, out string username, out string stamp)
    {
        username = stamp = "";
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 2048)
        {
            return false;
        }

        try
        {
            var values = _protector.Unprotect(ticket).Split('\n');
            if (values.Length != 2)
            {
                return false;
            }

            (username, stamp) = (values[0], values[1]);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public void Send(string email, string username, string temporaryPassword, string temporaryHash)
    {
        var publicUrl = configuration["Hosting:PublicBaseUrl"] ?? configuration["RENDER_EXTERNAL_URL"];
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps
            || origin.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("Cần cấu hình Hosting:PublicBaseUrl với địa chỉ HTTPS của hệ thống.");
        }

        var ticket = _protector.Protect($"{username}\n{PasswordHelper.SessionStamp(temporaryHash)}", TimeSpan.FromHours(24));
        var url = new Uri(origin, "/Account/Activate?ticket=" + Uri.EscapeDataString(ticket));
        emailService.SendEmail(email, "Đặt mật khẩu tài khoản nhân sự",
            $"<p>Tài khoản: {WebUtility.HtmlEncode(username)}</p>" +
            $"<p>Mật khẩu tạm: {WebUtility.HtmlEncode(temporaryPassword)}</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(url.AbsoluteUri)}\">Đặt mật khẩu</a></p>" +
            "<p>Liên kết và mật khẩu tạm có hiệu lực trong 24 giờ.</p>");
    }
}
