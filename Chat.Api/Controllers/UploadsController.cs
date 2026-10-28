using Chat.Api.Data;
using Chat.Api.Models;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public sealed class UploadsController(ChatDbContext db, IFileStorage storage) : ControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf", ".txt", ".csv", ".docx", ".xlsx", ".pptx", ".zip"
    };

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Empty file.");
        if (file.Length > MaxBytes) return BadRequest("Maximum file size is 10 MB.");

        var fileName = Path.GetFileName(file.FileName);
        if (!AllowedExtensions.Contains(Path.GetExtension(fileName)))
            return BadRequest("File type not allowed.");

        var key = Guid.NewGuid().ToString("N"); // random name; the original name is never used for storage
        await using (var stream = file.OpenReadStream())
            await storage.SaveAsync(key, stream, ct);

        var attachment = new ChatAttachment
        {
            FileName = fileName,
            StorageKey = key,
            ContentType = file.ContentType,
            Size = file.Length,
            UploaderId = User.Id()
        };
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        return Ok(new AttachmentDto
        {
            Id = attachment.Id, FileName = attachment.FileName,
            ContentType = attachment.ContentType, Size = attachment.Size
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var uid = User.Id();
        var a = await db.Attachments.Include(x => x.Message).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();

        var allowed = a.Message is null
            ? a.UploaderId == uid
            : await db.ConversationMembers.AnyAsync(m =>
                m.ConversationId == a.Message.ConversationId && m.UserId == uid, ct);
        if (!allowed) return Forbid();

        var stream = await storage.OpenReadAsync(a.StorageKey, ct);
        if (stream is null) return NotFound();

        // octet-stream forces a download so uploaded content is never rendered by the browser.
        return File(stream, "application/octet-stream", a.FileName);
    }
}
