using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Vtt.Api.Hubs;

/// <summary>Identifica o usuário do SignalR pela claim "sub" do JWT, para Clients.User(id) funcionar.</summary>
public class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
