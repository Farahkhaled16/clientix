namespace BrokerHub.Services;

public record Opt(string Key, string Ar, string En);

public static class Catalog
{
    public static readonly Opt[] Services =
    {
        new("Social Media", "سوشيال ميديا", "Social Media"),
        new("SEO", "تحسين محركات البحث", "SEO"),
        new("Branding", "هوية بصرية", "Branding"),
        new("Content", "صناعة محتوى", "Content"),
        new("Paid Ads", "إعلانات مدفوعة", "Paid Ads"),
        new("Video", "فيديو وموشن", "Video & Motion"),
        new("Influencers", "مؤثرين", "Influencers"),
        new("Web Design", "تصميم مواقع", "Web Design"),
        new("Email Marketing", "تسويق بالإيميل", "Email Marketing"),
        new("PR", "علاقات عامة", "PR"),
    };

    public static readonly Opt[] Budgets =
    {
        new("lt10", "أقل من 10 آلاف جنيه", "Under 10k EGP"),
        new("10-30", "من 10 إلى 30 ألف جنيه", "10k to 30k EGP"),
        new("30-100", "من 30 إلى 100 ألف جنيه", "30k to 100k EGP"),
        new("gt100", "أكثر من 100 ألف جنيه", "Over 100k EGP"),
        new("unsure", "لسه مش محدد", "Not sure yet"),
    };

    public static readonly Opt[] Timelines =
    {
        new("asap", "في أسرع وقت (أقل من أسبوعين)", "ASAP (under 2 weeks)"),
        new("month", "خلال شهر", "Within a month"),
        new("quarter", "خلال 2 إلى 3 شهور", "Within 2 to 3 months"),
        new("flex", "مرن", "Flexible"),
    };

    public static string Label(Opt[] list, string key, string lang)
    {
        var o = list.FirstOrDefault(x => x.Key == key);
        return o == null ? key : (lang == "en" ? o.En : o.Ar);
    }

    public static string ServiceLabels(string keys, string lang) =>
        string.Join(lang == "en" ? ", " : "، ",
            (keys ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(k => Label(Services, k, lang)));
}