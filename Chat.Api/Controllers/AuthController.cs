using Chat.Api.Data;
using Chat.Api.Models;
using Chat.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Controllers;

public sealed record RegisterRequest(string UserName, string DisplayName, string Password);
public sealed record LoginRequest(string UserName, string Password);

/// <summary>Minimal built-in auth so the app works end to end. Swap for your real identity provider later.</summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    ChatDbContext db, TokenService tokens, IPasswordHasher<AppUser> hasher) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest r)
    {
        var userName = r.UserName?.Trim().ToLowerInvariant() ?? "";
        var display = r.DisplayName?.Trim() ?? "";

        if (userName.Length is < 3 or > 50) return BadRequest("Username must be 3-50 characters.");
        if (display.Length is < 1 or > 100) return BadRequest("Display name is required.");
        if ((r.Password?.Length ?? 0) < 8) return BadRequest("Password must be at least 8 characters.");
        if (await db.Users.AnyAsync(u => u.UserName == userName)) return Conflict("Username already taken.");

        var user = new AppUser { UserName = userName, DisplayName = display };
        user.PasswordHash = hasher.HashPassword(user, r.Password!);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new AuthResult(tokens.Create(user), user.Id, user.DisplayName));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest r)
    {
        var userName = r.UserName?.Trim().ToLowerInvariant() ?? "";
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserName == userName);

        if (user is null ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, r.Password ?? "") == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid username or password.");

        return Ok(new AuthResult(tokens.Create(user), user.Id, user.DisplayName));
    }
}
