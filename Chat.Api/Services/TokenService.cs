using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chat.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;

namespace Chat.Api.Services;

public sealed class TokenService(IConfiguration cfg)
{
    public string Create(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: new[] { new Claim("sub", user.Id), new Claim("name", user.DisplayName) },
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class UserExtensions
{
    public static string Id(this ClaimsPrincipal p) =>
        p.FindFirstValue("sub") ?? throw new UnauthorizedAccessException();

    public static string DisplayName(this ClaimsPrincipal p) => p.FindFirstValue("name") ?? "User";
}

/// <summary>Lets Clients.User(id) target the JWT "sub" claim.</summary>
public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}
