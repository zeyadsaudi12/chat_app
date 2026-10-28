using Chat.Api.Data;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(ChatDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q = "")
    {
        var me = User.Id();
        q = q.Trim().ToLowerInvariant();

        var users = await db.Users
            .Where(u => u.Id != me && (q == "" || u.UserName.Contains(q) || u.DisplayName.ToLower().Contains(q)))
            .OrderBy(u => u.DisplayName)
            .Take(20)
            .Select(u => new { id = u.Id, userName = u.UserName, displayName = u.DisplayName })
            .ToListAsync();

        return Ok(users);
    }
}
