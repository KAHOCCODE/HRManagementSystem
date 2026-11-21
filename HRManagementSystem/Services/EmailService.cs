using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

public class EmailService
{
    internal const string HttpClientName = "EmailDelivery";
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public void SendEmail(string toEmail, string subject, string body, byte[]? attachmentBytes = null, string? attachmentName = null)
    {
        var resendKey = _config["EmailSettings:ResendApiKey"];
        if (!string.IsNullOrWhiteSpace(resendKey))
        {
            SendViaResend(resendKey, toEmail, subject, body, attachmentBytes, attachmentName);
            return;
        }

        var smtpServer = _config["EmailSettings:SmtpServer"];
        var port = int.Parse(_config["EmailSettings:Port"]);
        var senderEmail = _config["EmailSettings:SenderEmail"];
        var senderPassword = _config["EmailSettings:SenderPassword"];

        using var smtpClient = new SmtpClient(smtpServer)
        {
            Port = port,
            Credentials = new NetworkCredential(senderEmail, senderPassword),
            EnableSsl = true,
            UseDefaultCredentials = false
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(senderEmail),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        mailMessage.To.Add(toEmail);

        if (attachmentBytes != null && attachmentName != null)
        {
            var attachment = new Attachment(new MemoryStream(attachmentBytes), attachmentName);
            mailMessage.Attachments.Add(attachment);
        }

        smtpClient.Send(mailMessage);
    }
    private void SendViaResend(string apiKey, string recipient, string subject, string html,
        byte[]? attachmentBytes, string? attachmentName)
    {
        var sender = _config["EmailSettings:SenderEmail"];
        if (string.IsNullOrWhiteSpace(sender))
            throw new InvalidOperationException("Thiếu địa chỉ email người gửi.");

        var message = new Dictionary<string, object>
        {
            ["from"] = sender,
            ["to"] = new[] { recipient },
            ["subject"] = subject,
            ["html"] = html
        };

        if (attachmentBytes != null && attachmentName != null)
        {
            message["attachments"] = new[]
            {
                new { filename = attachmentName, content = Convert.ToBase64String(attachmentBytes) }
            };
        }

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(message);
        using var response = client.SendAsync(request).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

}
