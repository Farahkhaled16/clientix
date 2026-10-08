namespace BrokerHub.Services;

public class FileStorage
{
    private readonly IWebHostEnvironment _env;
    public FileStorage(IWebHostEnvironment env) => _env = env;

    private static readonly Dictionary<string, string> Kinds = new()
    {
        [".jpg"] = "image",
        [".jpeg"] = "image",
        [".png"] = "image",
        [".webp"] = "image",
        [".gif"] = "image",
        [".mp4"] = "video",
        [".webm"] = "video",
        [".mov"] = "video",
        [".pdf"] = "file",
        [".doc"] = "file",
        [".docx"] = "file",
        [".ppt"] = "file",
        [".pptx"] = "file"
    };

    public async Task<(string Url, string Kind)?> Save(IFormFile? f)
    {
        if (f == null || f.Length == 0) return null;
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        if (!Kinds.TryGetValue(ext, out var kind)) return null;   // نوع غير مسموح

        var dir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + ext;           // اسم عشوائي للأمان
        await using var fs = File.Create(Path.Combine(dir, name));
        await f.CopyToAsync(fs);
        return ($"/uploads/{name}", kind);
    }
}