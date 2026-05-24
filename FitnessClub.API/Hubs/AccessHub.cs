using Microsoft.AspNetCore.SignalR;

namespace FitnessClub.API.Hubs;

public class AccessHub : Hub
{
    // Термінали підключаються до групи "terminals"
    public async Task JoinTerminal()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "terminals");
    }
}