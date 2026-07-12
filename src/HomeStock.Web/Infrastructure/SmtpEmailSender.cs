using System.Net;
using System.Net.Mail;
using HomeStock.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HomeStock.Web.Infrastructure;

/// <summary>
/// Sends Identity emails (confirmation, password reset) over SMTP. Failures are logged rather
/// than thrown so account flows degrade gracefully if the mail server is unreachable.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    : IEmailSender<ApplicationUser>
{
    private readonly EmailOptions _options = options.Value;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your HomeStock account",
            $"Confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your HomeStock password",
            $"Reset your password by <a href=\"{resetLink}\">clicking here</a>. If you didn't request this, ignore this email.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Reset your HomeStock password",
            $"Your password reset code is: <strong>{resetCode}</strong>");

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.From!, _options.FromName ?? "HomeStock"),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.UseSsl };
            if (!string.IsNullOrWhiteSpace(_options.Username))
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);

            await client.SendMailAsync(message);
            logger.LogInformation("Sent '{Subject}' email to {To}", subject, to);
        }
        catch (Exception ex)
        {
            // Never surface SMTP details to the user; the account flow shows a generic message.
            logger.LogError(ex, "Failed to send '{Subject}' email to {To}", subject, to);
        }
    }
}
