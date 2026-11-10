namespace RaceDay.Api.Services;

// Saves uploaded images into the API's wwwroot/uploads folder and
// returns the web address to store in the database.
public class ImageStorage
{
    private const long MaxBytes = 2 * 1024 * 1024;   // images may be at most 2 MB
    private readonly string _uploadsRoot;

    public ImageStorage(IWebHostEnvironment env)
    {
        // wwwroot is the folder ASP.NET serves to browsers as static files
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _uploadsRoot = Path.Combine(webRoot, "uploads");
    }

    // Saves the file in wwwroot/uploads/<folder>/. Returns the web address, or an error message.
    public async Task<(string? Url, string? Error)> SaveAsync(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0)
            return (null, "Please choose an image file.");

        if (file.Length > MaxBytes)
            return (null, "The image must be 2 MB or smaller.");

        if (file.Length < 12)
            return (null, "That file is not a valid image.");

        // Read the first bytes to see what the file really is,
        // instead of trusting its name (anyone can rename virus.exe to photo.jpg)
        var header = new byte[12];
        await using (var peek = file.OpenReadStream())
        {
            await peek.ReadExactlyAsync(header);
        }

        var extension = DetectExtension(header);
        if (extension == null)
            return (null, "Only JPG, PNG, GIF or WebP images are allowed.");

        var folderPath = Path.Combine(_uploadsRoot, folder);
        Directory.CreateDirectory(folderPath);

        // A random file name avoids clashes and stops anyone choosing where the file goes
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using (var stream = new FileStream(Path.Combine(folderPath, fileName), FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return ($"/uploads/{folder}/{fileName}", null);
    }

    // Deletes an old image (used when a new one replaces it)
    public void Delete(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/"))
            return;

        var relative = url.Substring("/uploads/".Length).Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(_uploadsRoot) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(_uploadsRoot, relative));

        // Safety check: only ever delete files that are really inside the uploads folder
        if (full.StartsWith(root) && File.Exists(full))
            File.Delete(full);
    }

    // Looks at the first bytes of the file to decide its real type
    private static string? DetectExtension(byte[] h)
    {
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8') return ".gif";
        if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
            && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
    }
}