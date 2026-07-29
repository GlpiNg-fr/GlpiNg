using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages;

public partial class Home
{
    private int _computerCount;
    private int _agentCount;

    protected override async Task OnInitializedAsync()
    {
        _computerCount = await Db.Computers.CountAsync();
        _agentCount = await Db.Agents.CountAsync();
    }
}
