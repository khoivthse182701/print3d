using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Services;

public sealed class EmailSender(
    IConfiguration configuration,
    IHostEnvironment environment,
    IApplicationDbContext db,
    IClock clock,
    ILogger<EmailSender> logger) : IEmailSender
{
    public async Task SendVerificationEmailAsync(string email, string fullName, string token, CancellationToken ct)
    {
        // Enqueue outbox notification message atomically
        var outbox = OutboxMessage.Create(
            "Notification.Email.Verification",
            Guid.NewGuid(),
            $"email:verify:{email}:{Guid.NewGuid():N}",
            System.Text.Json.JsonSerializer.Serialize(new { email, fullName, purpose = "VerifyEmail" }),
            clock.UtcNow
        );
        db.OutboxMessages.Add(outbox);
        await db.SaveChangesAsync(ct);

        var host = configuration["MailSettings:Host"];
        if (string.IsNullOrWhiteSpace(host) || host.Trim().StartsWith('<'))
        {
            host = "smtp.gmail.com";
        }
        var user = configuration["MailSettings:Username"];
        var pass = configuration["MailSettings:Password"]?.Replace(" ", "").Trim();

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation("[DEV-SINK] Verification email dispatched for {Email}. Token: {Token}", email, token);
            }
            return;
        }

        try
        {
            var port = configuration.GetValue<int?>("MailSettings:Port") ?? 587;
            var enableSsl = configuration.GetValue<bool?>("MailSettings:EnableSsl") ?? true;
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = enableSsl
            };

            var senderEmail = configuration["MailSettings:SenderEmail"] ?? user;
            var senderName = configuration["MailSettings:SenderName"] ?? "THE BOYS - 3D AI Platform";

            var fromAddress = new MailAddress(senderEmail, senderName, Encoding.UTF8);
            var toAddress = new MailAddress(email, fullName, Encoding.UTF8);

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = "Xác nhận địa chỉ email của bạn - THE BOYS 3D",
                SubjectEncoding = Encoding.UTF8,
                HeadersEncoding = Encoding.UTF8
            };

            var plainText = $"Xin chào {fullName},\n\nMã xác thực tài khoản THE BOYS 3D của bạn là: {token}\n\nMã có hiệu lực trong vòng 24 giờ.\n\nTrân trọng,\nĐội ngũ THE BOYS 3D";
            var plainView = AlternateView.CreateAlternateViewFromString(plainText, new ContentType("text/plain; charset=utf-8"));
            plainView.TransferEncoding = TransferEncoding.Base64;
            message.AlternateViews.Add(plainView);

            var htmlBody = BuildVerificationEmailHtml(fullName, token);
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, new ContentType("text/html; charset=utf-8"));
            htmlView.TransferEncoding = TransferEncoding.Base64;
            message.AlternateViews.Add(htmlView);

            await client.SendMailAsync(message, ct);
            logger.LogInformation("Đã gửi email xác thực thành công tới {Email}", email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể gửi email xác thực qua SMTP.");
        }
    }

    public async Task SendPasswordResetEmailAsync(string email, string fullName, string token, CancellationToken ct)
    {
        var outbox = OutboxMessage.Create(
            "Notification.Email.PasswordReset",
            Guid.NewGuid(),
            $"email:reset:{email}:{Guid.NewGuid():N}",
            System.Text.Json.JsonSerializer.Serialize(new { email, fullName, purpose = "ResetPassword" }),
            clock.UtcNow
        );
        db.OutboxMessages.Add(outbox);
        await db.SaveChangesAsync(ct);

        var host = configuration["MailSettings:Host"];
        if (string.IsNullOrWhiteSpace(host) || host.Trim().StartsWith('<'))
        {
            host = "smtp.gmail.com";
        }
        var user = configuration["MailSettings:Username"];
        var pass = configuration["MailSettings:Password"]?.Replace(" ", "").Trim();

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation("[DEV-SINK] Password reset email dispatched for {Email}. Token: {Token}", email, token);
            }
            return;
        }

        try
        {
            var port = configuration.GetValue<int?>("MailSettings:Port") ?? 587;
            var enableSsl = configuration.GetValue<bool?>("MailSettings:EnableSsl") ?? true;
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = enableSsl
            };

            var senderEmail = configuration["MailSettings:SenderEmail"] ?? user;
            var senderName = configuration["MailSettings:SenderName"] ?? "THE BOYS - 3D AI Platform";

            var fromAddress = new MailAddress(senderEmail, senderName, Encoding.UTF8);
            var toAddress = new MailAddress(email, fullName, Encoding.UTF8);

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = "Yêu cầu đặt lại mật khẩu - THE BOYS 3D",
                SubjectEncoding = Encoding.UTF8,
                HeadersEncoding = Encoding.UTF8
            };

            var plainText = $"Xin chào {fullName},\n\nMã đặt lại mật khẩu THE BOYS 3D của bạn là: {token}\n\nMã có hiệu lực trong vòng 15 phút.\n\nTrân trọng,\nĐội ngũ THE BOYS 3D";
            var plainView = AlternateView.CreateAlternateViewFromString(plainText, new ContentType("text/plain; charset=utf-8"));
            plainView.TransferEncoding = TransferEncoding.Base64;
            message.AlternateViews.Add(plainView);

            var htmlBody = BuildPasswordResetEmailHtml(fullName, token);
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, new ContentType("text/html; charset=utf-8"));
            htmlView.TransferEncoding = TransferEncoding.Base64;
            message.AlternateViews.Add(htmlView);

            await client.SendMailAsync(message, ct);
            logger.LogInformation("Đã gửi email đặt lại mật khẩu thành công tới {Email}", email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể gửi email đặt lại mật khẩu qua SMTP.");
        }
    }

    private static string BuildVerificationEmailHtml(string fullName, string token)
    {
        var safeName = WebUtility.HtmlEncode(fullName);
        var safeToken = WebUtility.HtmlEncode(token);

        return $"""
<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Xác nhận địa chỉ email - THE BOYS 3D</title>
</head>
<body style="margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#0f172a;">
  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f1f5f9;padding:40px 15px;">
    <tr>
      <td align="center">
        <table role="presentation" width="100%" style="max-width:580px;background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px -5px rgba(0,0,0,0.1),0 8px 10px -6px rgba(0,0,0,0.1);border:1px solid #e2e8f0;" cellspacing="0" cellpadding="0">
          
          <!-- Banner / Header -->
          <tr>
            <td style="background:linear-gradient(135deg,#0f172a 0%,#1e1b4b 50%,#312e81 100%);padding:36px 30px;text-align:center;">
              <div style="display:inline-block;padding:6px 14px;background:rgba(255,255,255,0.1);border-radius:20px;border:1px solid rgba(255,255,255,0.2);margin-bottom:10px;">
                <span style="color:#a5b4fc;font-size:11px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;">3D AI Generative Platform</span>
              </div>
              <h1 style="margin:0;font-size:26px;font-weight:800;letter-spacing:1px;color:#ffffff;">THE BOYS <span style="color:#818cf8;">3D</span></h1>
            </td>
          </tr>

          <!-- Main Content -->
          <tr>
            <td style="padding:36px 32px 28px 32px;">
              <p style="margin:0 0 16px 0;font-size:16px;line-height:26px;color:#1e293b;font-weight:600;">
                Xin chào {safeName},
              </p>
              <p style="margin:0 0 20px 0;font-size:15px;line-height:24px;color:#475569;">
                Chào mừng bạn đến với <strong>THE BOYS 3D</strong>! Để hoàn tất đăng ký tài khoản và nhận ngay <strong>2 credit dùng thử miễn phí</strong> để trải nghiệm tạo mô hình 3D, vui lòng nhập mã xác thực gồm 6 ký tự dưới đây:
              </p>

              <!-- OTP / Token Card -->
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:28px 0;">
                <tr>
                  <td align="center" style="background:#f8fafc;border:2px dashed #6366f1;border-radius:12px;padding:20px 16px;">
                    <div style="font-size:12px;text-transform:uppercase;color:#6366f1;font-weight:700;letter-spacing:1.5px;margin-bottom:12px;">
                      MÃ XÁC THỰC CỦA BẠN
                    </div>
                    <div style="font-family:'Courier New',Consolas,monospace;font-size:38px;font-weight:800;color:#1e1b4b;letter-spacing:8px;word-break:keep-all;user-select:all;">
                      {safeToken}
                    </div>
                  </td>
                </tr>
              </table>

              <p style="margin:0 0 20px 0;font-size:14px;line-height:22px;color:#64748b;text-align:center;">
                ⏳ Mã xác thực có hiệu lực trong vòng <strong>24 giờ</strong>.
              </p>

              <!-- Security Notice -->
              <div style="background-color:#fffbeb;border-left:4px solid #f59e0b;border-radius:8px;padding:14px 16px;margin-top:20px;">
                <p style="margin:0;font-size:13px;line-height:20px;color:#92400e;">
                  <strong>Lưu ý bảo mật:</strong> Tuyệt đối không chia sẻ mã xác thực này với bất kỳ ai. Nếu bạn không đăng ký tài khoản tại THE BOYS 3D, vui lòng an tâm bỏ qua email này.
                </p>
              </div>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style="background-color:#f8fafc;padding:20px 32px;border-top:1px solid #e2e8f0;text-align:center;">
              <p style="margin:0 0 6px 0;font-size:13px;font-weight:600;color:#475569;">Đội ngũ phát triển THE BOYS 3D</p>
              <p style="margin:0;font-size:12px;line-height:18px;color:#94a3b8;">
                © 2026 THE BOYS 3D Platform. Mọi quyền được bảo lưu.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>
""";
    }

    private static string BuildPasswordResetEmailHtml(string fullName, string token)
    {
        var safeName = WebUtility.HtmlEncode(fullName);
        var safeToken = WebUtility.HtmlEncode(token);

        return $"""
<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Khôi phục mật khẩu - THE BOYS 3D</title>
</head>
<body style="margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#0f172a;">
  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f1f5f9;padding:40px 15px;">
    <tr>
      <td align="center">
        <table role="presentation" width="100%" style="max-width:580px;background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px -5px rgba(0,0,0,0.1),0 8px 10px -6px rgba(0,0,0,0.1);border:1px solid #e2e8f0;" cellspacing="0" cellpadding="0">
          
          <!-- Banner / Header -->
          <tr>
            <td style="background:linear-gradient(135deg,#0f172a 0%,#311010 50%,#450a0a 100%);padding:36px 30px;text-align:center;">
              <div style="display:inline-block;padding:6px 14px;background:rgba(255,255,255,0.1);border-radius:20px;border:1px solid rgba(255,255,255,0.2);margin-bottom:10px;">
                <span style="color:#fca5a5;font-size:11px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;">Bảo mật tài khoản</span>
              </div>
              <h1 style="margin:0;font-size:26px;font-weight:800;letter-spacing:1px;color:#ffffff;">THE BOYS <span style="color:#f87171;">3D</span></h1>
            </td>
          </tr>

          <!-- Main Content -->
          <tr>
            <td style="padding:36px 32px 28px 32px;">
              <p style="margin:0 0 16px 0;font-size:16px;line-height:26px;color:#1e293b;font-weight:600;">
                Xin chào {safeName},
              </p>
              <p style="margin:0 0 20px 0;font-size:15px;line-height:24px;color:#475569;">
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn tại <strong>THE BOYS 3D</strong>. Vui lòng sử dụng mã bảo mật gồm 6 ký tự dưới đây để thiết lập mật khẩu mới:
              </p>

              <!-- OTP / Token Card -->
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:28px 0;">
                <tr>
                  <td align="center" style="background:#fef2f2;border:2px dashed #ef4444;border-radius:12px;padding:20px 16px;">
                    <div style="font-size:12px;text-transform:uppercase;color:#dc2626;font-weight:700;letter-spacing:1.5px;margin-bottom:12px;">
                      MÃ ĐẶT LẠI MẬT KHẨU
                    </div>
                    <div style="font-family:'Courier New',Consolas,monospace;font-size:38px;font-weight:800;color:#991b1b;letter-spacing:8px;word-break:keep-all;user-select:all;">
                      {safeToken}
                    </div>
                  </td>
                </tr>
              </table>

              <p style="margin:0 0 20px 0;font-size:14px;line-height:22px;color:#64748b;text-align:center;">
                ⏳ Mã bảo mật có hiệu lực trong vòng <strong>15 phút</strong>.
              </p>

              <!-- Security Notice -->
              <div style="background-color:#fffbeb;border-left:4px solid #f59e0b;border-radius:8px;padding:14px 16px;margin-top:20px;">
                <p style="margin:0;font-size:13px;line-height:20px;color:#92400e;">
                  <strong>Cảnh báo bảo mật:</strong> Nếu bạn không yêu cầu đặt lại mật khẩu, tài khoản của bạn vẫn được bảo vệ an toàn. Bạn vui lòng bỏ qua thư này.
                </p>
              </div>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style="background-color:#f8fafc;padding:20px 32px;border-top:1px solid #e2e8f0;text-align:center;">
              <p style="margin:0 0 6px 0;font-size:13px;font-weight:600;color:#475569;">Đội ngũ phát triển THE BOYS 3D</p>
              <p style="margin:0;font-size:12px;line-height:18px;color:#94a3b8;">
                © 2026 THE BOYS 3D Platform. Mọi quyền được bảo lưu.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>
""";
    }
}
