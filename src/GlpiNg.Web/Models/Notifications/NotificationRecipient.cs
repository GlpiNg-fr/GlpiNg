using GlpiNg.Web.Models;

namespace GlpiNg.Web.Models.Notifications;

/// <summary>
/// Type de destinataire d'une <see cref="Notification"/>. Réduit par rapport aux ~20 types de
/// GLPI (Demandeur/Observateur/Technicien/... — tous liés aux acteurs d'un Ticket, module absent
/// de GlpiNg) aux seuls types que le domaine actuel de GlpiNg peut réellement résoudre.
/// </summary>
public enum NotificationRecipientType
{
    /// <summary>Adresse e-mail saisie librement (pas de compte GlpiNg associé).</summary>
    FixedEmail = 0,

    /// <summary>Un <see cref="GlpiUser"/> précis (voir <see cref="UserId"/>).</summary>
    User = 1,

    /// <summary>Tous les membres d'un <see cref="GlpiGroup"/> (voir <see cref="GroupId"/>) — le groupe doit avoir <see cref="GlpiGroup.CanBeNotified"/>.</summary>
    Group = 2,

    /// <summary>Tous les utilisateurs actifs avec <see cref="GlpiUser.IsAdmin"/>.</summary>
    AllAdmins = 3,
}

/// <summary>Destinataire d'une <see cref="Notification"/> — voir <see cref="NotificationRecipientType"/> pour les variantes.</summary>
public class NotificationRecipient
{
    public int Id { get; set; }

    public int NotificationId { get; set; }
    public Notification? Notification { get; set; }

    public NotificationRecipientType Type { get; set; }

    public string? Email { get; set; }

    public int? UserId { get; set; }
    public GlpiUser? User { get; set; }

    public int? GroupId { get; set; }
    public GlpiGroup? Group { get; set; }
}
