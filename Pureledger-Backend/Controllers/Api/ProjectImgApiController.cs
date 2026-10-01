using System.Buffers.Binary;
using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Auth;
using PureLedger.Services.Interface;

namespace PureLedger.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProjectImgApiController : ControllerBase
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private readonly LedgerDbContext _context;
    private readonly IAuthService _auth;
    private readonly ICurrentUserService _currentUser;

    public ProjectImgApiController(LedgerDbContext context, IAuthService auth, ICurrentUserService currentUser)
        => (_context, _auth, _currentUser) = (context, auth, currentUser);

    public sealed class SaveProjectImgRequest
    {
        public string? ImageDataUrl { get; set; }
    }

    [HttpGet("{projectId:int}")]
    public async Task<IActionResult> Get(int projectId)
    {
        var access = await CheckAccessAsync(projectId, write: false);
        if (access != null) return access;

        var image = await _context.ProjectImgs.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.ImageDataUrl)
            .SingleOrDefaultAsync();
        return Ok(new { success = true, data = new { projectId, imageDataUrl = image } });
    }

    [HttpPut("{projectId:int}")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Put(int projectId, [FromBody] SaveProjectImgRequest request)
    {
        var access = await CheckAccessAsync(projectId, write: true);
        if (access != null) return access;

        if (!IsValidImage(request.ImageDataUrl))
            return BadRequest(new { success = false, message = "Upload a valid PNG or JPEG logo up to 5 MB." });

        var existing = await _context.ProjectImgs.FindAsync(projectId);
        if (existing == null)
            _context.ProjectImgs.Add(new ProjectImg { ProjectId = projectId, ImageDataUrl = request.ImageDataUrl! });
        else
            existing.ImageDataUrl = request.ImageDataUrl!;

        await _context.SaveChangesAsync();
        return Ok(new { success = true, data = new { projectId, imageDataUrl = request.ImageDataUrl } });
    }

    [HttpDelete("{projectId:int}")]
    public async Task<IActionResult> Delete(int projectId)
    {
        var access = await CheckAccessAsync(projectId, write: true);
        if (access != null) return access;

        var existing = await _context.ProjectImgs.FindAsync(projectId);
        if (existing != null)
        {
            _context.ProjectImgs.Remove(existing);
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true });
    }

    private async Task<IActionResult?> CheckAccessAsync(int projectId, bool write)
    {
        var userId = _currentUser.GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();
        if (!await _context.Projects.AsNoTracking().AnyAsync(x => x.ProjectId == projectId))
            return NotFound(new { success = false, message = "Project not found." });
        if (!await _auth.HasProjectAccessAsync(userId.Value, projectId))
            return Forbid();

        if (write && !_currentUser.IsCurrentUserAdmin() &&
            !await _auth.HasControllerAccessAsync(userId.Value, "MetaData") &&
            !await _auth.HasActionAccessAsync(userId.Value, "MetaData", "Projects"))
            return Forbid();

        return null;
    }

    private static bool IsValidImage(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return false;
        const string pngPrefix = "data:image/png;base64,";
        const string jpegPrefix = "data:image/jpeg;base64,";
        var isPng = dataUrl.StartsWith(pngPrefix, StringComparison.Ordinal);
        var isJpeg = dataUrl.StartsWith(jpegPrefix, StringComparison.Ordinal);
        if (!isPng && !isJpeg) return false;

        var base64 = dataUrl[(isPng ? pngPrefix.Length : jpegPrefix.Length)..];
        if (base64.Length == 0 || base64.Length > ((MaxImageBytes + 2) / 3) * 4) return false;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length == 0 || bytes.Length > MaxImageBytes) return false;
            return isPng ? IsValidPng(bytes) : IsValidJpeg(bytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidPng(byte[] bytes)
    {
        if (bytes.Length < 45 ||
            !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return false;

        using var compressed = new MemoryStream();
        var position = 8;
        var hasHeader = false;
        var hasData = false;
        var hasEnd = false;
        while (position + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
            position += 4;
            var type = bytes.AsSpan(position, 4);
            position += 4;
            if (length > MaxImageBytes || length > bytes.Length - position - 4) return false;

            if (type.SequenceEqual("IHDR"u8))
            {
                if (hasHeader || position != 16 || length != 13) return false;
                var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
                var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position + 4, 4));
                if (width == 0 || height == 0 || width > 4096 || height > 4096) return false;
                hasHeader = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (!hasHeader || hasEnd) return false;
                compressed.Write(bytes, position, (int)length);
                hasData = true;
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (length != 0 || !hasData) return false;
                hasEnd = true;
            }

            position += (int)length + 4; // Skip data and CRC.
            if (hasEnd) break;
        }
        if (!hasEnd || position != bytes.Length) return false;

        compressed.Position = 0;
        try
        {
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            Span<byte> buffer = stackalloc byte[8192];
            var decompressed = 0;
            int read;
            while ((read = zlib.Read(buffer)) > 0)
            {
                decompressed += read;
                if (decompressed > 64 * 1024 * 1024) return false;
            }
            return decompressed > 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsValidJpeg(byte[] bytes) =>
        bytes.Length >= 10 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 &&
        bytes[^2] == 255 && bytes[^1] == 217 &&
        bytes.AsSpan().IndexOf(new byte[] { 255, 218 }) >= 0;
}
