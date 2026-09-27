using GlpiNg.Web.Models.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GlpiNg.Web.Services.Notifications;

/// <summary>
/// Envoie un e-mail via le serveur SMTP configuré sur la page /config/notifications (voir
/// <see cref="NotificationSettings"/>). Utilisé à la fois par le bouton "Tester l'envoi" de
/// cette page (synchrone, retour immédiat à l'admin) et par
/// <see cref="QueuedNotificationSenderCronTask"/> (asynchrone, un envoi par ligne de
/// <see cref="QueuedNotification"/>). MailKit plutôt que System.Net.Mail.SmtpClient — voir le
/// commentaire sur la référence de paquet dans GlpiNg.Web.csproj.
/// </summary>
public class SmtpMailSender(AuthSecretProtector secretProtector)
{
    public async Task SendAsync(NotificationSettings settings, string toEmail, string subject, string? textBody, string? htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            throw new InvalidOperationException("Aucun serveur SMTP configuré (voir /config/notifications).");
        }

        if (string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            throw new InvalidOperationException("Aucune adresse d'expédition configurée (voir /config/notifications).");
        }

        MimeMessage message = new();
        message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        if (!string.IsNullOrWhiteSpace(settings.ReplyToEmail))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(settings.ReplyToEmail));
        }

        message.Subject = subject;

        BodyBuilder builder = new();
        if (!string.IsNullOrWhiteSpace(htmlBody)) builder.HtmlBody = htmlBody;
        if (!string.IsNullOrWhiteSpace(textBody)) builder.TextBody = textBody;
        if (builder.HtmlBody is null && builder.TextBody is null) builder.TextBody = string.Empty;
        message.Body = builder.ToMessageBody();

        SecureSocketOptions socketOptions = settings.Encryption switch
        {
            SmtpEncryptionMode.Ssl => SecureSocketOptions.SslOnConnect,
            SmtpEncryptionMode.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.None
        };

        using SmtpClient client = new();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, socketOptions, cancellationToken);

        if (settings.RequiresAuthentication && !string.IsNullOrWhiteSpace(settings.SmtpUsername))
        {
            string password = secretProtector.Unprotect(settings.SmtpPasswordProtected) ?? string.Empty;
            await client.AuthenticateAsync(settings.SmtpUsername, password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
