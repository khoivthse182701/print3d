namespace TheBoys.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendVerificationEmailAsync(string email, string fullName, string token, CancellationToken ct);
    Task SendPasswordResetEmailAsync(string email, string fullName, string token, CancellationToken ct);
}

