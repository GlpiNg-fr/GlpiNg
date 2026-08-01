namespace GlpiNg.Web.Models;

/// <summary>Une ligne de l'onglet "Historique" : trace d'une modification enregistrée depuis la page /config.</summary>
public record HistoryEntry(int Id, DateTimeOffset Date, string User, string Field, string Update);
