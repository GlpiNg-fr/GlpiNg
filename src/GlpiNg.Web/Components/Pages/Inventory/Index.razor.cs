using System.IO.Compression;
using System.Text.Json;
using BlazorBootstrap;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Agent;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Inventory;

public partial class Index : ComponentBase
{
    private const string SectionName = "InventorySettings";
    private const long MaxFileSize = 20 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private InventorySettings? _model;
    private bool _isSaving;

    private bool _isImporting;

    protected override async Task OnInitializedAsync()
    {
        _model = await SettingsStore.ReadSectionAsync<InventorySettings>(SectionName);
    }

    private async Task SaveAsync()
    {
        if (_model is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            var before = await SettingsStore.ReadSectionAsync<InventorySettings>(SectionName);
            await SettingsStore.SaveSectionAsync(SectionName, _model);
            await History.AppendAsync("Administrateur", SettingsDiff.Compare(before, _model));
            ToastService.Notify(new ToastMessage(ToastType.Success, "Configuration enregistrée."));
        }
        catch (IOException ex)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, $"Échec de l'enregistrement : {ex.Message}"));
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task OnFilesSelectedAsync(InputFileChangeEventArgs e)
    {
        _isImporting = true;
        StateHasChanged();

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
            InventoryImportService importer = new(db, SettingsStore, NotificationDispatch, DeploymentAssignmentService, EntityTree, HttpContextAccessor);

            foreach (IBrowserFile file in e.GetMultipleFiles(10))
            {
                if (file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await ImportZipAsync(file, importer);
                }
                else
                {
                    await ImportSingleFileAsync(file, importer);
                }
            }
        }
        finally
        {
            _isImporting = false;
        }
    }

    private async Task ImportZipAsync(IBrowserFile file, InventoryImportService importer)
    {
        try
        {
            await using Stream stream = file.OpenReadStream(MaxFileSize);
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;

            using ZipArchive archive = new(buffer, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await using Stream entryStream = entry.Open();
                using StreamReader reader = new(entryStream);
                string json = await reader.ReadToEndAsync();
                await ImportJsonAsync(entry.FullName, json, importer);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, file.Name, $"Archive invalide : {ex.Message}"));
        }
    }

    private async Task ImportSingleFileAsync(IBrowserFile file, InventoryImportService importer)
    {
        if (!file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, file.Name, "Format non supporté (seuls .json et .zip sont acceptés)."));
            return;
        }

        await using Stream stream = file.OpenReadStream(MaxFileSize);
        using StreamReader reader = new(stream);
        string json = await reader.ReadToEndAsync();
        await ImportJsonAsync(file.Name, json, importer);
    }

    private async Task ImportJsonAsync(string fileName, string json, InventoryImportService importer)
    {
        string? deviceId = null;
        InventoryContent? content = null;

        // "content" et "deviceid" sont des propriétés "required" sur InventoryRequest : un
        // fichier qui contient directement l'objet content (sans enveloppe deviceid/action)
        // échoue donc cette désérialisation — on retombe alors sur InventoryContent seul, avec
        // un identifiant dérivé de l'UUID matériel ou, à défaut, du nom de fichier.
        try
        {
            InventoryRequest request = JsonSerializer.Deserialize<InventoryRequest>(json, JsonOptions)!;
            deviceId = request.DeviceId;
            content = request.Content;
        }
        catch (JsonException)
        {
            try
            {
                content = JsonSerializer.Deserialize<InventoryContent>(json, JsonOptions);
                deviceId = content?.Hardware?.Uuid ?? Path.GetFileNameWithoutExtension(fileName);
            }
            catch (JsonException ex)
            {
                ToastService.Notify(new ToastMessage(ToastType.Danger, fileName, $"JSON invalide : {ex.Message}"));
                return;
            }
        }

        if (content is null || string.IsNullOrWhiteSpace(deviceId))
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, fileName, "Fichier illisible : format d'inventaire GLPI-Agent non reconnu."));
            return;
        }

        Computer? computer = await importer.ImportFromDeviceIdAsync(deviceId, content);
        if (computer is null)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, fileName, "Rejeté par une règle d'affectation à l'import (voir Administration > Règles d'import > Historique des refus)."));
            return;
        }

        ToastService.Notify(new ToastMessage(ToastType.Success, fileName, $"Importé : {computer.Name} (#{computer.Id})."));
    }
}
