using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Computers;

public partial class Index : ComponentBase
{
    private enum SortField
    {
        Name,
        Status,
        Manufacturer,
        OperatingSystem,
        LastInventory
    }

    private enum SearchFieldType
    {
        Text,
        Select,
        Number,
        Date
    }

    private sealed record SearchFieldDefinition(string Key, string Label, SearchFieldType Type, (string Value, string Label)[]? Options = null);

    private sealed class SearchCriterion
    {
        public string Link { get; set; } = "AND";
        public string FieldKey { get; set; } = "all";
        public string Operator { get; set; } = "contains";
        public string Value { get; set; } = string.Empty;
    }

    private sealed class SortCriterion
    {
        public SortField Field { get; set; }
        public bool Descending { get; set; }
    }

    private static readonly (string Value, string Label)[] StatusOptions =
    [
        (nameof(ComputerStatus.InStock), "En stock"),
        (nameof(ComputerStatus.InProduction), "En production"),
        (nameof(ComputerStatus.Broken), "En panne"),
        (nameof(ComputerStatus.Retired), "Réformé")
    ];

    private static readonly Dictionary<SearchFieldType, (string Value, string Label)[]> OperatorsByType = new()
    {
        [SearchFieldType.Text] =
        [
            ("contains", "contient"),
            ("notcontains", "ne contient pas"),
            ("equals", "est"),
            ("notequals", "n'est pas"),
            ("empty", "est vide")
        ],
        [SearchFieldType.Select] =
        [
            ("equals", "est"),
            ("notequals", "n'est pas")
        ],
        [SearchFieldType.Number] =
        [
            ("equals", "est"),
            ("notequals", "n'est pas"),
            ("greaterthan", "supérieur à"),
            ("lessthan", "inférieur à"),
            ("empty", "est vide")
        ],
        [SearchFieldType.Date] =
        [
            ("equals", "est"),
            ("before", "avant le"),
            ("after", "après le"),
            ("empty", "est vide")
        ]
    };

    private static readonly SearchFieldDefinition[] SearchFields =
    [
        new("all", "Tous les champs", SearchFieldType.Text),
        new("name", "Nom", SearchFieldType.Text),
        new("status", "Statut", SearchFieldType.Select, StatusOptions),
        new("manufacturer", "Fabricant", SearchFieldType.Text),
        new("model", "Modèle", SearchFieldType.Text),
        new("serial", "Numéro de série", SearchFieldType.Text),
        new("chassistype", "Type", SearchFieldType.Text),
        new("os", "Système d'exploitation", SearchFieldType.Text),
        new("osversion", "Version de l'OS", SearchFieldType.Text),
        new("oskernel", "Version du noyau", SearchFieldType.Text),
        new("uuid", "UUID matériel", SearchFieldType.Text),
        new("vmsystem", "Système de virtualisation", SearchFieldType.Text),
        new("site", "Site", SearchFieldType.Text),
        new("building", "Bâtiment", SearchFieldType.Text),
        new("room", "Salle", SearchFieldType.Text),
        new("assigneduser", "Usager", SearchFieldType.Text),
        new("lastloggeduser", "Dernier utilisateur connecté", SearchFieldType.Text),
        new("memory", "Mémoire (Mo)", SearchFieldType.Number),
        new("lastinventory", "Dernière remontée", SearchFieldType.Date),
        new("createdat", "Date de création", SearchFieldType.Date)
    ];

    private static readonly (SortField Field, string Label)[] SortableColumns =
    [
        (SortField.Name, "Nom"),
        (SortField.Status, "Statut"),
        (SortField.Manufacturer, "Fabricant / Modèle"),
        (SortField.OperatingSystem, "Système d'exploitation"),
        (SortField.LastInventory, "Dernière remontée")
    ];

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    private static readonly int[] PageSizeOptions = [25, 50, 100, 200, 500];

    private List<Computer> _computers = [];
    private List<Computer> _filteredComputers = [];
    private List<Computer> _pagedComputers = [];
    private readonly List<SearchCriterion> _criteria = [new()];
    private readonly List<SortCriterion> _sortCriteria = [new() { Field = SortField.Name, Descending = false }];
    private readonly HashSet<int> _selectedIds = [];
    private int _pageSize = 25;
    private int _currentPage = 1;

    private bool AllSelected => _pagedComputers.Count > 0 && _selectedIds.IsSupersetOf(_pagedComputers.Select(computer => computer.Id));

    private int TotalPages => _filteredComputers.Count == 0 ? 1 : (int)Math.Ceiling(_filteredComputers.Count / (double)_pageSize);

    private int ActiveCriteriaCount => _criteria.Count(criterion => criterion.Operator == "empty" || !string.IsNullOrWhiteSpace(criterion.Value));

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _computers = await db.Computers
            .AsNoTracking()
            .OrderBy(computer => computer.Name)
            .ToListAsync();

        _selectedIds.Clear();
        _currentPage = 1;
        ApplyFilterAndSort();
    }

    private async Task OnRefreshAsync()
    {
        await LoadAsync();
    }

    private static SearchFieldDefinition? FindField(string key)
    {
        return SearchFields.FirstOrDefault(field => field.Key == key);
    }

    private static (string Value, string Label)[] GetOperators(SearchFieldDefinition? field)
    {
        if (field is null) return [];
        if (field.Key == "all") return [("contains", "contient"), ("notcontains", "ne contient pas")];
        return OperatorsByType[field.Type];
    }

    private void OnCriterionFieldChanged(SearchCriterion criterion, string fieldKey)
    {
        criterion.FieldKey = fieldKey;
        (string Value, string Label)[] operators = GetOperators(FindField(fieldKey));
        criterion.Operator = operators.Length > 0 ? operators[0].Value : "contains";
        criterion.Value = string.Empty;
    }

    private void AddCriterion()
    {
        _criteria.Add(new SearchCriterion());
    }

    private void RemoveCriterion(SearchCriterion criterion)
    {
        if (_criteria.Count <= 1) return;
        _criteria.Remove(criterion);
        ApplySearch();
    }

    private void ApplySearch()
    {
        _currentPage = 1;
        ApplyFilterAndSort();
    }

    private void ResetSearch()
    {
        _criteria.Clear();
        _criteria.Add(new SearchCriterion());
        ApplySearch();
    }

    private void SetPageSize(int pageSize)
    {
        if (_pageSize == pageSize) return;

        _pageSize = pageSize;
        _currentPage = 1;
        ApplyPaging();
    }

    private void GoToPage(int page)
    {
        int targetPage = Math.Clamp(page, 1, TotalPages);
        if (targetPage == _currentPage) return;

        _currentPage = targetPage;
        ApplyPaging();
    }

    private void SetSort(SortField field, bool additive)
    {
        SortCriterion? existing = _sortCriteria.FirstOrDefault(criterion => criterion.Field == field);

        if (additive)
        {
            if (existing is not null)
            {
                existing.Descending = !existing.Descending;
            }
            else
            {
                _sortCriteria.Add(new SortCriterion { Field = field, Descending = false });
            }
        }
        else if (_sortCriteria.Count == 1 && existing is not null)
        {
            existing.Descending = !existing.Descending;
        }
        else
        {
            _sortCriteria.Clear();
            _sortCriteria.Add(new SortCriterion { Field = field, Descending = false });
        }

        _currentPage = 1;
        ApplyFilterAndSort();
    }

    private SortCriterion? GetSortCriterion(SortField field)
    {
        return _sortCriteria.FirstOrDefault(criterion => criterion.Field == field);
    }

    private void ApplyFilterAndSort()
    {
        List<Computer> matched = _computers;
        bool first = true;

        foreach (SearchCriterion criterion in _criteria)
        {
            if (criterion.Operator != "empty" && string.IsNullOrWhiteSpace(criterion.Value)) continue;

            List<Computer> criterionMatches = _computers.Where(computer => EvaluateCriterion(computer, criterion)).ToList();

            if (first)
            {
                matched = criterionMatches;
                first = false;
            }
            else if (criterion.Link == "OR")
            {
                matched = matched.Union(criterionMatches).ToList();
            }
            else
            {
                matched = matched.Intersect(criterionMatches).ToList();
            }
        }

        IOrderedEnumerable<Computer>? ordered = null;
        foreach (SortCriterion criterion in _sortCriteria)
        {
            ordered = ordered is null
                ? (criterion.Descending
                    ? matched.OrderByDescending(computer => GetSortKey(computer, criterion.Field))
                    : matched.OrderBy(computer => GetSortKey(computer, criterion.Field)))
                : (criterion.Descending
                    ? ordered.ThenByDescending(computer => GetSortKey(computer, criterion.Field))
                    : ordered.ThenBy(computer => GetSortKey(computer, criterion.Field)));
        }

        _filteredComputers = (ordered ?? matched.AsEnumerable()).ToList();
        _selectedIds.IntersectWith(_filteredComputers.Select(computer => computer.Id));
        ApplyPaging();
    }

    private void ApplyPaging()
    {
        _currentPage = Math.Clamp(_currentPage, 1, TotalPages);
        _pagedComputers = _filteredComputers
            .Skip((_currentPage - 1) * _pageSize)
            .Take(_pageSize)
            .ToList();
    }

    private static IComparable GetSortKey(Computer computer, SortField field) => field switch
    {
        SortField.Status => computer.Status,
        SortField.Manufacturer => ManufacturerAndModel(computer),
        SortField.OperatingSystem => computer.OperatingSystem ?? string.Empty,
        SortField.LastInventory => computer.LastInventoryAt ?? DateTime.MinValue,
        _ => computer.Name
    };

    private void ToggleSelectAll(bool selectAll)
    {
        foreach (Computer computer in _pagedComputers)
        {
            if (selectAll)
            {
                _selectedIds.Add(computer.Id);
            }
            else
            {
                _selectedIds.Remove(computer.Id);
            }
        }
    }

    private void ToggleSelect(int computerId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(computerId);
        }
        else
        {
            _selectedIds.Remove(computerId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0) return;

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<Computer> toDelete = await db.Computers
            .Where(computer => _selectedIds.Contains(computer.Id))
            .ToListAsync();

        db.Computers.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private static bool EvaluateCriterion(Computer computer, SearchCriterion criterion)
    {
        SearchFieldDefinition? field = FindField(criterion.FieldKey);
        if (field is null) return true;

        if (field.Key == "all")
        {
            string term = criterion.Value.Trim();
            if (term.Length == 0) return true;

            bool anyMatch = GetAllFieldsText(computer).Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase));
            return criterion.Operator == "notcontains" ? !anyMatch : anyMatch;
        }

        return field.Type switch
        {
            SearchFieldType.Text => EvaluateText(GetFieldText(computer, field.Key), criterion),
            SearchFieldType.Select => EvaluateSelect(GetFieldSelectValue(computer, field.Key), criterion),
            SearchFieldType.Number => EvaluateNumber(GetFieldNumber(computer, field.Key), criterion),
            SearchFieldType.Date => EvaluateDate(GetFieldDate(computer, field.Key), criterion),
            _ => true
        };
    }

    private static bool EvaluateText(string? raw, SearchCriterion criterion)
    {
        if (criterion.Operator == "empty") return string.IsNullOrWhiteSpace(raw);

        string value = raw ?? string.Empty;
        string term = criterion.Value.Trim();
        return criterion.Operator switch
        {
            "contains" => value.Contains(term, StringComparison.OrdinalIgnoreCase),
            "notcontains" => !value.Contains(term, StringComparison.OrdinalIgnoreCase),
            "equals" => string.Equals(value, term, StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(value, term, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static bool EvaluateSelect(string current, SearchCriterion criterion)
    {
        bool isEqual = string.Equals(current, criterion.Value, StringComparison.Ordinal);
        return criterion.Operator == "notequals" ? !isEqual : isEqual;
    }

    private static bool EvaluateNumber(double? raw, SearchCriterion criterion)
    {
        if (criterion.Operator == "empty") return raw is null;
        if (raw is null) return false;
        if (!double.TryParse(criterion.Value, out double target)) return true;

        return criterion.Operator switch
        {
            "equals" => raw.Value == target,
            "notequals" => raw.Value != target,
            "greaterthan" => raw.Value > target,
            "lessthan" => raw.Value < target,
            _ => true
        };
    }

    private static bool EvaluateDate(DateTime? raw, SearchCriterion criterion)
    {
        if (criterion.Operator == "empty") return raw is null;
        if (raw is null) return false;
        if (!DateTime.TryParse(criterion.Value, out DateTime target)) return true;

        DateTime rawDate = raw.Value.Date;
        DateTime targetDate = target.Date;
        return criterion.Operator switch
        {
            "equals" => rawDate == targetDate,
            "before" => rawDate < targetDate,
            "after" => rawDate > targetDate,
            _ => true
        };
    }

    private static IEnumerable<string> GetAllFieldsText(Computer computer)
    {
        if (!string.IsNullOrWhiteSpace(computer.Name)) yield return computer.Name;
        if (!string.IsNullOrWhiteSpace(computer.Manufacturer)) yield return computer.Manufacturer;
        if (!string.IsNullOrWhiteSpace(computer.Model)) yield return computer.Model;
        if (!string.IsNullOrWhiteSpace(computer.OperatingSystem)) yield return computer.OperatingSystem;
        if (!string.IsNullOrWhiteSpace(computer.SerialNumber)) yield return computer.SerialNumber;
    }

    private static string? GetFieldText(Computer computer, string key) => key switch
    {
        "name" => computer.Name,
        "manufacturer" => computer.Manufacturer,
        "model" => computer.Model,
        "serial" => computer.SerialNumber,
        "chassistype" => computer.ChassisType,
        "os" => computer.OperatingSystem,
        "osversion" => computer.OsVersion,
        "oskernel" => computer.OsKernelVersion,
        "uuid" => computer.HardwareUuid,
        "vmsystem" => computer.VmSystem,
        "site" => computer.Site,
        "building" => computer.Building,
        "room" => computer.Room,
        "assigneduser" => computer.AssignedUser,
        "lastloggeduser" => computer.LastLoggedUser,
        _ => null
    };

    private static string GetFieldSelectValue(Computer computer, string key) => key switch
    {
        "status" => computer.Status.ToString(),
        _ => string.Empty
    };

    private static double? GetFieldNumber(Computer computer, string key) => key switch
    {
        "memory" => computer.TotalMemoryMb,
        _ => null
    };

    private static DateTime? GetFieldDate(Computer computer, string key) => key switch
    {
        "lastinventory" => computer.LastInventoryAt,
        "createdat" => computer.CreatedAt,
        _ => null
    };

    private static string ManufacturerAndModel(Computer computer)
    {
        bool hasManufacturer = !string.IsNullOrWhiteSpace(computer.Manufacturer);
        bool hasModel = !string.IsNullOrWhiteSpace(computer.Model);

        if (!hasManufacturer && !hasModel) return "—";
        if (hasManufacturer && hasModel) return $"{computer.Manufacturer} {computer.Model}";
        return hasManufacturer ? computer.Manufacturer! : computer.Model!;
    }

    private static string LastInventoryLabel(Computer computer)
    {
        return computer.LastInventoryAt is { } lastInventory
            ? lastInventory.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "Jamais";
    }

    private static string StatusLabel(ComputerStatus status) => status switch
    {
        ComputerStatus.InStock => "En stock",
        ComputerStatus.InProduction => "En production",
        ComputerStatus.Broken => "En panne",
        ComputerStatus.Retired => "Réformé",
        _ => status.ToString()
    };

    private static string StatusCssClass(ComputerStatus status) => status switch
    {
        ComputerStatus.InStock => "glpi-status-instock",
        ComputerStatus.InProduction => "glpi-status-inproduction",
        ComputerStatus.Broken => "glpi-status-broken",
        ComputerStatus.Retired => "glpi-status-retired",
        _ => "bg-secondary"
    };
}
