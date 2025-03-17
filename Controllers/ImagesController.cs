using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace ZeniControlSuite.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ImagesController : ControllerBase
{
    [HttpGet("{imageName}")]
    public IActionResult GetImage([FromQuery] string imageName)
    {
        //Console.WriteLine($"Requested {imageName}");

        string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images/"+imageName);
        //Console.WriteLine($"Path: {imagePath}");

        if (!System.IO.File.Exists(imagePath))
        {
            return NotFound();
        }

        var image = System.IO.File.OpenRead(imagePath);
        return File(image, "image/png");
    }
}