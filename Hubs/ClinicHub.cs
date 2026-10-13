using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace StudentManagement.Hubs;

public class ClinicHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var identity = Context.User?.Identity;
        if (identity?.IsAuthenticated == true)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }

            if (!string.IsNullOrEmpty(role))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, role);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomName)
    {
        if (!string.IsNullOrWhiteSpace(roomName))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName.Trim());
        }
    }

    public async Task LeaveRoom(string roomName)
    {
        if (!string.IsNullOrWhiteSpace(roomName))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName.Trim());
        }
    }

    public async Task JoinDoctorRoom(int doctorId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doctor_{doctorId}");
    }

    public async Task JoinUserRoom(int userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }

    public async Task JoinRoleRoom(string role)
    {
        if (!string.IsNullOrWhiteSpace(role))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, role.Trim());
        }
    }
}
