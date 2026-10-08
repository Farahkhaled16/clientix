using System.Text;
using BrokerHub.Models;

namespace BrokerHub.Services;

public static class Ics
{
    private static string Esc(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");

    private static string Fmt(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'");

    private static void Event(StringBuilder sb, Meeting m, string summary, string description,
                              string alarmText, bool tentative)
    {
        void L(string s) => sb.Append(s).Append("\r\n");

        L("BEGIN:VEVENT");
        L($"UID:{m.Id}@clientix");
        L($"DTSTAMP:{Fmt(DateTime.UtcNow)}");
        L($"DTSTART:{Fmt(m.StartsAt)}");
        L($"DTEND:{Fmt(m.StartsAt.AddMinutes(30))}");
        L($"SUMMARY:{Esc(summary)}");
        L($"DESCRIPTION:{Esc(description)}");
        L(tentative ? "STATUS:TENTATIVE" : "STATUS:CONFIRMED");
        if (!tentative)
        {
            foreach (var trigger in new[] { "-P1D", "-PT1H" })   // تنبيه قبل يوم وقبل ساعة
            {
                L("BEGIN:VALARM");
                L($"TRIGGER:{trigger}");
                L("ACTION:DISPLAY");
                L($"DESCRIPTION:{Esc(alarmText)}");
                L("END:VALARM");
            }
        }
        L("END:VEVENT");
    }

    private static StringBuilder Header(string? calName = null, bool feed = false)
    {
        var sb = new StringBuilder();
        void L(string s) => sb.Append(s).Append("\r\n");
        L("BEGIN:VCALENDAR");
        L("VERSION:2.0");
        L("PRODID:-//Clientix//Meetings//EN");
        L("CALSCALE:GREGORIAN");
        L("METHOD:PUBLISH");
        if (calName != null) L($"X-WR-CALNAME:{Esc(calName)}");
        if (feed)
        {
            L("REFRESH-INTERVAL;VALUE=DURATION:PT1H");
            L("X-PUBLISHED-TTL:PT1H");
        }
        return sb;
    }

    // ميعاد واحد (مرفق الإيميل وزرار أضف للتقويم)
    public static byte[] ForMeeting(Meeting m, Translator t, string lang)
    {
        var who = string.IsNullOrEmpty(m.AgencyName) ? t.Get("meet.general", lang) : m.AgencyName;
        return Build(m, t.Get("meet.ics.title", lang), $"{who} - {m.BusinessName}", t.Get("meet.ics.alarm", lang));
    }

    public static byte[] Build(Meeting m, string summary, string description, string alarmText)
    {
        var sb = Header();
        Event(sb, m, summary, description, alarmText, false);
        sb.Append("END:VCALENDAR\r\n");
        return new UTF8Encoding(false).GetBytes(sb.ToString());
    }

    // اشتراك التقويم: كل مواعيد المستخدم
    public static byte[] Feed(IEnumerable<Meeting> meetings, string calName,
                              Func<Meeting, (string summary, string desc, bool tentative)> describe, string alarmText)
    {
        var sb = Header(calName, feed: true);
        foreach (var m in meetings)
        {
            var d = describe(m);
            Event(sb, m, d.summary, d.desc, alarmText, d.tentative);
        }
        sb.Append("END:VCALENDAR\r\n");
        return new UTF8Encoding(false).GetBytes(sb.ToString());
    }

    // لينك "أضف لجوجل كالندر"
    public static string GoogleUrl(Meeting m, string title, string details) =>
        "https://calendar.google.com/calendar/render?action=TEMPLATE" +
        $"&text={Uri.EscapeDataString(title)}" +
        $"&dates={Fmt(m.StartsAt)}/{Fmt(m.StartsAt.AddMinutes(30))}" +
        $"&details={Uri.EscapeDataString(details)}";
}