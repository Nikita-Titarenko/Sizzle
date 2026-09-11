using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using Sizzle.Application.Services;
using Sizzle.Infrastructure.Options;

namespace Sizzle.Infrastructure.Services;

public class SmtpEmailSender(IOptions<SmtpEmailOptions> options) : IEmailSender
{
    private const string SenderName = "Sizzle";
    private readonly SmtpEmailOptions _options = options.Value;

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.Email))
        {
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(SenderName, _options.Email));
        message.To.Add(new MailboxAddress(string.Empty, email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlMessage };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_options.Host, _options.Port, MailKit.Security.SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_options.Email, _options.Password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }
}
