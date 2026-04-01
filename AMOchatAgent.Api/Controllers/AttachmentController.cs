using AMOchatAgent.Api.Models;
using AMOchatAgent.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AMOchatAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttachmentController : ControllerBase
{
    private readonly AttachmentStore _store;
    private readonly ILogger<AttachmentController> _logger;

    // Max 10 MB per file
    private const long MaxFileSize = 10 * 1024 * 1024;

    public AttachmentController(AttachmentStore store, ILogger<AttachmentController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        if (file.Length > MaxFileSize)
            return BadRequest(new { error = "File exceeds 10 MB limit" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var attachment = new StoredAttachment
        {
            Id = Guid.NewGuid().ToString("N"),
            FileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
            Data = ms.ToArray(),
            Size = file.Length
        };

        _store.Save(attachment);
        _logger.LogInformation("Attachment uploaded: {Id} {FileName} {Size}B", attachment.Id, attachment.FileName, attachment.Size);

        return Ok(new AttachmentUploadResponse
        {
            AttachmentId = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            Size = attachment.Size
        });
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        _store.Delete(id);
        return NoContent();
    }
}
