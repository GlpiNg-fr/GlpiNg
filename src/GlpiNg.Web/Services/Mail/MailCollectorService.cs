using GlpiNg.Modules.Abstractions.Documents;
using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Abstractions.Notifications;
using GlpiNg.Modules.Assistance.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Import;
using GlpiNg.Web.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace GlpiNg.Web.Services.Mail;

/// <summary>
/// Relève une boîte IMAP (<see cref="MailCollector"/>) et ouvre un ticket par message.
///
/// La non-duplication repose sur le drapeau « lu » du serveur : seuls les messages non lus sont
/// relevés, et un message n'est marqué lu (puis rangé, si un dossier est indiqué) qu'une fois son
/// ticket écrit — jamais avant, pour qu'une panne en cours de route le laisse à reprendre.
/// ponytail : un humain qui lit la boîte depuis son client mail fait donc manquer le message ;
/// tenir une table des Message-Id relevés serait le remède si la boîte n'est pas dédiée.
///
/// Appelé par <see cref="MailCollectorCronTask"/> et par le bouton « Relever maintenant » de la
/// fiche, d'où le compte rendu renvoyé à l'appelant *et* inscrit sur le collecteur.
/// </summary>
public class MailCollectorService(
    GlpiNgDbContext db,
    AuthSecretProtector protector,
    IDocumentAttachments documents,
    INotificationPublisher notifications,
    SettingsCacheService settingsStore,
    ILogger<MailCollectorService> logger)
{
    /// <summary>Auteur porté par l'historique des tickets ouverts par une relève : ce n'est personne.</summary>
    private const string CollectorUser = "Collecteur";

    /// <summary>
    /// Relève un collecteur, désigné par son identifiant : le collecteur est relu par le contexte
    /// de ce service, seul à pouvoir en enregistrer le compte rendu — une instance venue d'un
    /// autre contexte (celui d'une page) n'y serait pas suivie, et la mise à jour serait perdue.
    ///
    /// Les erreurs sont inscrites sur le collecteur et renvoyées plutôt que levées : un serveur
    /// injoignable ne doit pas faire échouer la relève des autres.
    /// </summary>
    public async Task<(int Imported, string? Error)> CollectAsync(int collectorId, CancellationToken cancellationToken = default)
    {
        MailCollector? collector = await db.MailCollectors.FirstOrDefaultAsync(c => c.Id == collectorId, cancellationToken);

        if (collector is null)
        {
            return (0, "Collecteur introuvable.");
        }

        int imported = 0;
        string? error = null;

        try
        {
            imported = await FetchAsync(collector, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            error = exception.Message;
            logger.LogWarning(exception, "Relève du collecteur « {Collector} » en échec.", collector.Name);
        }

        collector.LastRunAt = DateTime.UtcNow;
        collector.LastImportedCount = imported;
        collector.LastError = error;
        await db.SaveChangesAsync(cancellationToken);

        return (imported, error);
    }

    private async Task<int> FetchAsync(MailCollector collector, CancellationToken cancellationToken)
    {
        long maxAttachmentSize = collector.ImportAttachments
            ? (await settingsStore.ReadSectionAsync<AssistanceSettings>("AssistanceSettings")).MailCollectorMaxFileSize
            : 0;

        using ImapClient client = new();

        if (!collector.ValidateCertificate)
        {
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        }

        SecureSocketOptions socketOptions = collector.Encryption switch
        {
            MailServerEncryption.Ssl => SecureSocketOptions.SslOnConnect,
            MailServerEncryption.Tls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.None,
        };

        await client.ConnectAsync(collector.Host, collector.Port ?? 0, socketOptions, cancellationToken);
        await client.AuthenticateAsync(collector.Login, protector.Unprotect(collector.PasswordProtected) ?? string.Empty, cancellationToken);

        IMailFolder folder = await client.GetFolderAsync(collector.Folder, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        IMailFolder? destination = string.IsNullOrWhiteSpace(collector.MoveToFolder)
            ? null
            : await client.GetFolderAsync(collector.MoveToFolder, cancellationToken);

        IList<UniqueId> unread = await folder.SearchAsync(SearchQuery.NotSeen, cancellationToken);
        int imported = 0;

        foreach (UniqueId uid in unread.Take(Math.Max(1, collector.MaxFetchPerRun)))
        {
            MimeMessage message = await folder.GetMessageAsync(uid, cancellationToken);

            int ticketId = await OpenTicketAsync(collector, message, maxAttachmentSize, cancellationToken);

            // Rangement après écriture : un message marqué lu dont le ticket n'existe pas serait
            // perdu sans trace.
            await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);

            if (destination is not null)
            {
                await folder.MoveToAsync(uid, destination, cancellationToken);
            }

            imported++;

            logger.LogInformation(
                "Collecteur « {Collector} » : ticket {TicketId} ouvert depuis « {Subject} ».",
                collector.Name, ticketId, message.Subject);
        }

        await client.DisconnectAsync(true, cancellationToken);

        return imported;
    }

    private async Task<int> OpenTicketAsync(MailCollector collector, MimeMessage message, long maxAttachmentSize, CancellationToken cancellationToken)
    {
        MailboxAddress? sender = message.From.Mailboxes.FirstOrDefault();
        string? senderAddress = sender?.Address;

        // Le demandeur est reconnu à son adresse quand un compte la porte ; sinon l'adresse est
        // conservée en clair, qui reste la seule façon de lui répondre.
        int? requesterId = senderAddress is null
            ? null
            : await db.Users
                .Where(user => user.Email == senderAddress)
                .Select(user => (int?)user.Id)
                .FirstOrDefaultAsync(cancellationToken);

        Ticket ticket = new()
        {
            Name = string.IsNullOrWhiteSpace(message.Subject) ? "(sans objet)" : message.Subject.Trim(),
            Content = BodyOf(message),
            EntityId = collector.EntityId,
            RequesterUserId = requesterId,
            RequesterName = requesterId is null ? senderAddress : null,
            OpenedAt = message.Date == default ? DateTime.UtcNow : message.Date.UtcDateTime,
        };

        ticket.Priority = ItilPriorityMatrix.Compute(ticket.Urgency, ticket.Impact);

        db.Set<Ticket>().Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        db.Set<AssistanceHistoryEntry>().Add(new AssistanceHistoryEntry
        {
            ItemType = ItemTypes.Ticket,
            ItemId = ticket.Id,
            User = CollectorUser,
            Field = "Ticket",
            Description = $"Ouverture du ticket depuis le collecteur « {collector.Name} »",
        });

        await db.SaveChangesAsync(cancellationToken);

        if (maxAttachmentSize > 0)
        {
            await AttachAsync(ticket.Id, message, maxAttachmentSize, cancellationToken);
        }

        await PublishAsync(ticket, cancellationToken);

        return ticket.Id;
    }

    /// <summary>
    /// Corps du message en texte : la partie texte quand elle existe, sinon la partie HTML
    /// convertie — <c>Ticket.Content</c> est affiché tel quel, un message HTML y montrerait ses
    /// balises.
    /// </summary>
    private static string? BodyOf(MimeMessage message) =>
        message.TextBody ?? (message.HtmlBody is { } html ? GlpiHtmlToMarkdown.Convert(html) : null);

    private async Task AttachAsync(int ticketId, MimeMessage message, long maxAttachmentSize, CancellationToken cancellationToken)
    {
        foreach (MimePart part in message.Attachments.OfType<MimePart>())
        {
            string fileName = part.FileName ?? "piece-jointe";

            using MemoryStream buffer = new();
            await part.Content.DecodeToAsync(buffer, cancellationToken);

            // Au-delà de la taille permise par « Taille max. des pièces jointes du collecteur de
            // mails » (/config → Assistance), la pièce est passée : le ticket vaut mieux sans elle
            // que pas de ticket du tout.
            if (buffer.Length > maxAttachmentSize)
            {
                logger.LogInformation(
                    "Pièce jointe « {FileName} » ({Size} octets) écartée du ticket {TicketId} : au-delà de la taille permise.",
                    fileName, buffer.Length, ticketId);
                continue;
            }

            buffer.Position = 0;

            try
            {
                await documents.UploadAndAttachAsync(ItemTypes.Ticket, ticketId, fileName, buffer,
                    part.ContentType?.MimeType, CollectorUser, null, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Pièce jointe « {FileName} » non rattachée au ticket {TicketId}.", fileName, ticketId);
            }
        }
    }

    private async Task PublishAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        Dictionary<string, string?> variables = new(StringComparer.Ordinal)
        {
            ["ticket.id"] = ticket.Id.ToString(),
            ["ticket.title"] = ticket.Name,
            ["ticket.type"] = TicketLabels.For(ticket.Type),
            ["ticket.status"] = TicketLabels.For(ticket.Status),
            ["ticket.priority"] = ItilLabels.For(ticket.Priority),
            ["ticket.requester"] = ticket.RequesterName,
            ["ticket.url"] = $"/assistance/tickets/{ticket.Id}",
        };

        try
        {
            await notifications.PublishAsync(ItemTypes.Ticket, "new", ticket.Id, variables, cancellationToken);
        }
        catch (Exception exception)
        {
            // Le ticket est écrit : une notification manquée ne doit pas faire reprendre le message.
            logger.LogWarning(exception, "Notification d'ouverture non publiée pour le ticket {TicketId}.", ticket.Id);
        }
    }
}
