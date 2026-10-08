using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using BrokerHub.Models;

namespace BrokerHub.Services;

public class AutofillService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _c;
    public AutofillService(HttpClient http, IConfiguration c) { _http = http; _c = c; }

    private const string Prompt = @"Extract company information from the attached document (CV / company profile / portfolio).
Return ONLY a JSON object with exactly these keys:
CompanyName, About, Services, City, FoundedYear, TeamSize, Website, Instagram, LinkedIn.
Rules: keep the original language of the document. About = a short summary (max 4 sentences).
Services = comma separated list. FoundedYear and TeamSize are numbers or null. Use empty string if unknown. Never invent data.";

    private class Dto
    {
        public string? CompanyName { get; set; }
        public string? About { get; set; }
        public string? Services { get; set; }
        public string? City { get; set; }
        public int? FoundedYear { get; set; }
        public int? TeamSize { get; set; }
        public string? Website { get; set; }
        public string? Instagram { get; set; }
        public string? LinkedIn { get; set; }
    }

    public async Task<Portfolio?> Extract(IFormFile file)
    {
        try
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var parts = new List<object>();
            if (ext == ".pdf")
                parts.Add(new { inline_data = new { mime_type = "application/pdf", data = Convert.ToBase64String(bytes) } });
            else if (ext is ".jpg" or ".jpeg" or ".png" or ".webp")
            {
                var mt = ext == ".png" ? "image/png" : ext == ".webp" ? "image/webp" : "image/jpeg";
                parts.Add(new { inline_data = new { mime_type = mt, data = Convert.ToBase64String(bytes) } });
            }
            else if (ext == ".docx")
                parts.Add(new { text = "Document text:\n" + ReadDocx(bytes) });
            else return null;

            parts.Add(new { text = Prompt });

            var body = new
            {
                contents = new[] { new { parts } },
                generationConfig = new { responseMimeType = "application/json" }
            };

            var model = _c["Gemini:Model"] ?? "gemini-2.5-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("x-goog-api-key", _c["Gemini:ApiKey"]);
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var text = doc.RootElement.GetProperty("candidates")[0]
                          .GetProperty("content").GetProperty("parts")[0]
                          .GetProperty("text").GetString() ?? "";
            text = text.Replace("```json", "").Replace("```", "").Trim();

            var d = JsonSerializer.Deserialize<Dto>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (d == null) return null;

            return new Portfolio
            {
                CompanyName = d.CompanyName ?? "",
                About = d.About ?? "",
                Services = d.Services ?? "",
                City = d.City ?? "",
                FoundedYear = d.FoundedYear ?? 0,
                TeamSize = d.TeamSize ?? 0,
                Website = d.Website ?? "",
                Instagram = d.Instagram ?? "",
                LinkedIn = d.LinkedIn ?? ""
            };
        }
        catch { return null; }
    }

    private static string ReadDocx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var paras = doc.MainDocumentPart?.Document.Body?.Descendants<Paragraph>().Select(p => p.InnerText)
                    ?? Enumerable.Empty<string>();
        return string.Join("\n", paras);
    }
}