using Microsoft.AspNetCore.Mvc;

namespace ZeniControlSuite.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ImagesController : ControllerBase
{
    [HttpGet("{imageName}")]
    public IActionResult GetImage(string imageName)
    {
        return ServeImage(Path.Combine("Images", imageName));
    }

    [HttpGet("Avatars/{imageName}")]
    public IActionResult GetAvatarThumbnail(string imageName)
    {
        return ServeImage(Path.Combine("Images", "Avatars", imageName));
    }

    [HttpGet("Controls/{imageName}")]
    public IActionResult GetControlImage(string imageName)
    {
        return ServeImage(Path.Combine("Images", "Controls", imageName));
    }

    private IActionResult ServeImage(string relativePath)
    {
        var imagePath = ResolveImagePath(relativePath);
        if (imagePath == null)
        {
            return NotFound();
        }

        var image = System.IO.File.OpenRead(imagePath);
        return File(image, GetContentType(imagePath));
    }

    private static string? ResolveImagePath(string relativePath)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(Environment.CurrentDirectory, relativePath),
            Path.Combine(Environment.CurrentDirectory, "wwwroot", relativePath)
        };

        return candidates.FirstOrDefault(System.IO.File.Exists);
    }

    private static string GetContentType(string imagePath)
    {
        return Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "image/png"
        };
    }
}
