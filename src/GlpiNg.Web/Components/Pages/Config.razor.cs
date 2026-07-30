using GlpiNg.Web.Models;

namespace GlpiNg.Web.Components.Pages;

public partial class Config
{
    private ServerSettings? _settings;
    private bool _isSaving;
    private string? _statusMessage;

    protected override async Task OnInitializedAsync()
    {
        _settings = await SettingsStore.ReadAsync();
    }

    private async Task SaveAsync()
    {
        if (_settings is null)
        {
            return;
        }

        _isSaving = true;
        _statusMessage = null;

        try
        {
            await SettingsStore.SaveAsync(_settings);
            _statusMessage = "Configuration enregistrée.";
        }
        catch (IOException ex)
        {
            _statusMessage = $"Échec de l'enregistrement : {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }
}
