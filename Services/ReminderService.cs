namespace BrokerHub.Services;

public class ReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _sf;
    private readonly ILogger<ReminderService> _log;
    public ReminderService(IServiceScopeFactory sf, ILogger<ReminderService> log) { _sf = sf; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(20), ct); } catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try { await Tick(); }
            catch (Exception e) { _log.LogError(e, "Reminder tick failed"); }

            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task Tick()
    {
        using var scope = _sf.CreateScope();
        var fs = scope.ServiceProvider.GetRequiredService<FirestoreService>();
        var notify = scope.ServiceProvider.GetRequiredService<NotifyService>();

        var now = DateTime.UtcNow;
        var upcoming = await fs.GetMeetingsBetween(now, now.AddHours(25));

        foreach (var m in upcoming.Where(x => x.Status == "approved"))
        {
            var diff = m.StartsAt - now;
            if (diff <= TimeSpan.Zero) continue;

            var hourDue = !m.ReminderHourSent && diff <= TimeSpan.FromHours(1);
            var dayDue = !m.ReminderDaySent && diff <= TimeSpan.FromHours(24);
            if (!hourDue && !dayDue) continue;

            // نعلّم الأول عشان مايتبعتش مرتين، ولو الاتنين مستحقين نبعت تذكير الساعة بس
            await fs.SetReminderFlags(m.Id, true, hourDue || m.ReminderHourSent);
            await notify.Reminder(m, hourDue ? "hour" : "day");
        }
    }
}