using System.Text.Json.Nodes;
using GlpiNg.Web.Api.Glpi;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;

namespace GlpiNg.Web.Api;

/// <summary>
/// Profils vus par l'API : GlpiNg a un niveau de droit par grande section du menu, GLPI une
/// centaine de droits nommés (computer, ticket, user...). Chaque droit GLPI est rattaché à la
/// section qui couvre les objets qu'il protège, et prend la valeur maximale de GLPI (celle du
/// profil Super-Admin d'une installation neuve) en écriture, READ en lecture, 0 sinon.
/// </summary>
public static class GlpiApiProfiles
{
    /// <summary>Droit READ de GLPI.</summary>
    public const int Read = 1;

    private const int SuperAdminProfileId = 4;

    private static readonly Dictionary<string, ProfileSection?> Sections = BuildSections();

    private static Dictionary<string, ProfileSection?> BuildSections()
    {
        Dictionary<string, ProfileSection?> map = new(StringComparer.Ordinal);
        void Add(ProfileSection? section, params string[] names)
        {
            foreach (string name in names)
            {
                map[name] = section;
            }
        }

        Add(ProfileSection.Parc, "computer", "monitor", "software", "networking", "printer", "peripheral", "cartridge",
            "consumable", "phone", "internet", "device", "devicesimcard_pinpuk", "cable_management", "unmanaged",
            "refusedequipment", "locked_field", "agent");
        Add(ProfileSection.Gestion, "contact_enterprise", "document", "contract", "infocom", "budget", "license", "line",
            "certificate", "domain", "appliance", "cluster", "database", "datacenter");
        Add(ProfileSection.Assistance, "ticket", "followup", "task", "ticketvalidation", "change", "changevalidation",
            "problem", "planning", "ticketcost", "statistic", "ticketrecurrent", "recurrentchange", "externalevent",
            "show_group_hardware", "global_validation");
        Add(ProfileSection.Outils, "knowbase", "reservation", "reports", "project", "projecttask", "reminder_public",
            "rssfeed_public", "bookmark_public");
        Add(ProfileSection.Administration, "user", "entity", "group", "profile", "transfer", "logs", "system_logs",
            "backup", "inventory", "queuednotification");
        Add(ProfileSection.Configuration, "config", "dropdown", "location", "itilcategory", "knowbasecategory", "state",
            "typedoc", "link", "notification", "calendar", "slm", "search_config", "dashboard", "itiltemplate",
            "itilvalidationtemplate", "solutiontemplate", "itilfollowuptemplate", "tasktemplate", "taskcategory",
            "lineoperator", "pendingreason", "oauth_client", "snmpcredential", "form");
        // Droits personnels, accordés à tout compte.
        Add(null, "password_update", "personalization");
        return map;
    }

    /// <summary>Section GlpiNg couvrant un droit GLPI (les règles relèvent de l'administration).</summary>
    public static ProfileSection? SectionOf(string right)
        => Sections.TryGetValue(right, out ProfileSection? section)
            ? section
            : right.StartsWith("rule_", StringComparison.Ordinal) ? ProfileSection.Administration : ProfileSection.Configuration;

    public static ProfileRightLevel LevelOf(GlpiProfile profile, ProfileSection section) => section switch
    {
        ProfileSection.Parc => profile.ParcRight,
        ProfileSection.Assistance => profile.AssistanceRight,
        ProfileSection.Gestion => profile.GestionRight,
        ProfileSection.Outils => profile.OutilsRight,
        ProfileSection.Administration => profile.AdministrationRight,
        ProfileSection.Configuration => profile.ConfigurationRight,
        _ => ProfileRightLevel.None,
    };

    /// <summary>Droits GLPI nommés (glpi_profilerights) équivalents à des droits par section.</summary>
    public static JsonObject RightsOf(ProfileRights rights)
    {
        JsonObject result = [];
        JsonObject superAdmin = GlpiMetadata.Instance.Defaults["profilerights"]?[SuperAdminProfileId.ToString()]?.AsObject() ?? [];
        foreach ((string name, JsonNode? max) in superAdmin)
        {
            ProfileSection? section = SectionOf(name);
            int full = (int?)max ?? 0;
            result[name] = section is null
                ? full
                : rights.Level(section.Value) switch
                {
                    ProfileRightLevel.Write => full,
                    ProfileRightLevel.Read => full == 0 ? 0 : Read,
                    _ => 0,
                };
        }
        return result;
    }

    /// <summary>Ligne glpi_profiles d'un profil (champs de GLPI, valeurs par défaut de GLPI pour le reste).</summary>
    public static JsonObject ProfileRow(int id, string name, GlpiProfile? profile)
    {
        JsonObject defaults = GlpiMetadata.Instance.Defaults["profiles"]?.AsArray()
            .OfType<JsonObject>().FirstOrDefault(p => (int?)p["id"] == SuperAdminProfileId)?.DeepClone().AsObject() ?? [];
        JsonObject row = new() { ["id"] = id };
        foreach ((string key, JsonNode? value) in defaults)
        {
            if (key != "id")
            {
                row[key] = value?.DeepClone();
            }
        }
        row["name"] = name;
        row["interface"] = "central";
        row["is_default"] = profile?.IsDefault == true ? 1 : 0;
        row["comment"] = profile?.Comment ?? string.Empty;
        row["date_mod"] = profile is null ? null : Glpi.Data.GlpiValue.ToText(profile.UpdatedAt);
        row["date_creation"] = profile is null ? null : Glpi.Data.GlpiValue.ToText(profile.CreatedAt);
        return row;
    }
}
