using GlpiNg.Modules.Abstractions.Localization;
﻿using System.Text.RegularExpressions;
using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.ExternalLinks;
using GlpiNg.Modules.Inventory.Services;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.ExternalLinks;
using GlpiNg.Web.Services.ExternalLinks;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.ExternalLinks;

public partial class Detail : ComponentBase
{
    /// <summary>Repère les <c>[BALISE]</c> d'une URL, pour signaler celles qu'un type ne fournit pas.</summary>
    [GeneratedRegex(@"\[([A-Za-z_]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex TagPattern();

    [Parameter]
    public int LinkId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private ExternalLink? _link;
    private readonly HashSet<string> _selectedItemTypes = new(StringComparer.Ordinal);

    private bool _isSaving;
    private bool _isSavingItemTypes;
    private string? _previewItemType;

    /// <summary>Le type choisi pour l'aperçu, ou le premier coché si le choix n'est plus valable.</summary>
    private string PreviewItemType =>
        _previewItemType is { Length: > 0 } chosen && _selectedItemTypes.Contains(chosen)
            ? chosen
            : _selectedItemTypes.OrderBy(ExternalLinkTagSource.LabelFor).FirstOrDefault()
              ?? ExternalLinkTagSource.ComputerItemType;

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        _link = await db.ExternalLinks
            .AsNoTracking()
            .Include(link => link.ItemTypes)
            .FirstOrDefaultAsync(link => link.Id == LinkId);

        _selectedItemTypes.Clear();
        foreach (ExternalLinkItemType association in _link?.ItemTypes ?? [])
        {
            _selectedItemTypes.Add(association.ItemType);
        }
    }

    private void ToggleItemType(string itemType, bool selected)
    {
        if (selected)
        {
            _selectedItemTypes.Add(itemType);
        }
        else
        {
            _selectedItemTypes.Remove(itemType);
        }
    }

    private async Task SaveAsync()
    {
        if (_link is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

            ExternalLink? stored = await db.ExternalLinks.FirstOrDefaultAsync(link => link.Id == LinkId);
            if (stored is null)
            {
                return;
            }

            stored.Name = _link.Name;
            stored.Comment = _link.Comment;
            stored.Url = _link.Url;
            stored.OpenInNewWindow = _link.OpenInNewWindow;
            stored.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Lien enregistré.")));
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task SaveItemTypesAsync()
    {
        _isSavingItemTypes = true;

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

            List<ExternalLinkItemType> existing = await db.ExternalLinkItemTypes
                .Where(association => association.ExternalLinkId == LinkId)
                .ToListAsync();

            // Différentiel plutôt que « tout effacer puis tout réécrire » : les lignes inchangées
            // gardent leur identifiant, et une erreur en cours de route ne laisse pas le lien
            // détaché de tous ses types.
            foreach (ExternalLinkItemType association in existing.Where(a => !_selectedItemTypes.Contains(a.ItemType)))
            {
                db.ExternalLinkItemTypes.Remove(association);
            }

            HashSet<string> already = [.. existing.Select(association => association.ItemType)];

            foreach (string itemType in _selectedItemTypes.Where(itemType => !already.Contains(itemType)))
            {
                db.ExternalLinkItemTypes.Add(new ExternalLinkItemType
                {
                    ExternalLinkId = LinkId,
                    ItemType = itemType,
                });
            }

            await db.SaveChangesAsync();

            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Types associés enregistrés.")));
            await LoadAsync();
        }
        finally
        {
            _isSavingItemTypes = false;
        }
    }

    private async Task DeleteAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        ExternalLink? stored = await db.ExternalLinks.FirstOrDefaultAsync(link => link.Id == LinkId);
        if (stored is null)
        {
            return;
        }

        db.ExternalLinks.Remove(stored);
        await db.SaveChangesAsync();

        Navigation.NavigateTo("/config/external-links");
    }

    /// <summary>Balises écrites dans l'URL que le type de référence ne saura pas remplir.</summary>
    private List<string>? UnsupportedTagsUsed
    {
        get
        {
            if (_link is null || _selectedItemTypes.Count == 0)
            {
                return null;
            }

            HashSet<string> supported = new(ExternalLinkTagSource.SupportedTags(PreviewItemType), StringComparer.OrdinalIgnoreCase);

            return
            [
                .. TagPattern().Matches(_link.Url)
                    .Select(match => match.Groups[1].Value.ToUpperInvariant())
                    // Une balise inconnue de tout le catalogue n'est pas une balise : c'est un
                    // crochet que l'auteur a voulu là, et le rendu la laissera intacte.
                    .Where(tag => ExternalLinkTags.Catalog.Any(entry => string.Equals(entry.Tag, tag, StringComparison.OrdinalIgnoreCase)))
                    .Where(tag => !supported.Contains(tag))
                    .Distinct()
            ];
        }
    }

    /// <summary>URL rendue avec des valeurs d'exemple, pour juger de sa forme sans ouvrir une fiche.</summary>
    private string Preview
    {
        get
        {
            if (_link is null)
            {
                return string.Empty;
            }

            Dictionary<string, string?> sample = new(StringComparer.OrdinalIgnoreCase);

            foreach (string tag in ExternalLinkTagSource.SupportedTags(PreviewItemType))
            {
                sample[tag] = SampleValue(tag);
            }

            return ExternalLinkProvider.Substitute(_link.Url, sample);
        }
    }

    private static string SampleValue(string tag) => tag switch
    {
        ExternalLinkTags.Id => "42",
        ExternalLinkTags.ItemType => Tr.T("Computer"),
        ExternalLinkTags.Name => Tr.T("PC-042"),
        ExternalLinkTags.Serial => Tr.T("SN-ABC123"),
        ExternalLinkTags.OtherSerial => Tr.T("INV-0099"),
        ExternalLinkTags.Type => Tr.T("Laptop"),
        ExternalLinkTags.Model => Tr.T("Latitude 5540"),
        ExternalLinkTags.Manufacturer => Tr.T("Dell"),
        ExternalLinkTags.State => Tr.T("En service"),
        ExternalLinkTags.Location => Tr.T("Ingénierie"),
        ExternalLinkTags.User => "jdupont",
        ExternalLinkTags.Tech => "athibault",
        ExternalLinkTags.Uuid => "4c4c4544-0042-3010-8052-b9c04f503432",
        ExternalLinkTags.Comment => Tr.T("Poste de test"),
        ExternalLinkTags.Ip => "192.168.1.42",
        ExternalLinkTags.Mac => "00:1a:2b:3c:4d:5e",
        ExternalLinkTags.OperatingSystem => Tr.T("Windows 11"),
        ExternalLinkTags.Domain => "exemple.local",
        _ => tag,
    };

    private static string TagLabel(string tag) =>
        ExternalLinkTags.Catalog.FirstOrDefault(entry => entry.Tag == tag).Label ?? tag;

    /// <summary>Avertit sur la forme de l'URL sans rien interdire : un protocole maison reste légitime.</summary>
    private string? UrlWarning
    {
        get
        {
            if (_link is null || string.IsNullOrWhiteSpace(_link.Url))
            {
                return null;
            }

            // Les balises sont remplacées à l'affichage : on teste la forme sur une URL déjà
            // rendue, sinon les crochets feraient échouer l'analyse à tous les coups.
            string rendered = ExternalLinkProvider.Substitute(_link.Url, new Dictionary<string, string?>());

            return Uri.TryCreate(rendered, UriKind.Absolute, out _)
                ? null
                : Tr.T("Adresse non absolue : indiquez le schéma, par exemple https://exemple.org/…");
        }
    }
}
