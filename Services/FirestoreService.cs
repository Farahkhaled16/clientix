using Google.Cloud.Firestore;
using BrokerHub.Models;

namespace BrokerHub.Services;

public class FirestoreService
{
    public FirestoreDb Db { get; }

    public FirestoreService(IConfiguration config)
    {
        var keyPath = config["Firebase:KeyPath"]!;
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", keyPath);
        Db = FirestoreDb.Create(config["Firebase:ProjectId"]);
    }

    // ---------- Users ----------
    public async Task<AppUser?> GetUserByEmail(string email)
    {
        var snap = await Db.Collection("users")
            .WhereEqualTo("Email", email.Trim().ToLower())
            .Limit(1).GetSnapshotAsync();
        return snap.Documents.FirstOrDefault()?.ConvertTo<AppUser>();
    }

    public async Task CreateUser(AppUser user)
    {
        var doc = Db.Collection("users").Document();
        user.Id = doc.Id;
        await doc.SetAsync(user);
    }

    public async Task UpdateUser(AppUser user) =>
        await Db.Collection("users").Document(user.Id).SetAsync(user);

    public async Task<AppUser?> GetUserByField(string field, string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var snap = await Db.Collection("users")
            .WhereEqualTo(field, value).Limit(1).GetSnapshotAsync();
        return snap.Documents.FirstOrDefault()?.ConvertTo<AppUser>();
    }

    public async Task<AppUser?> GetUserById(string id)
    {
        var snap = await Db.Collection("users").Document(id).GetSnapshotAsync();
        return snap.Exists ? snap.ConvertTo<AppUser>() : null;
    }

    public async Task<List<AppUser>> GetAgencies()
    {
        var snap = await Db.Collection("users")
            .WhereEqualTo("Role", "agency")
            .WhereEqualTo("EmailConfirmed", true)
            .GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<AppUser>()).ToList();
    }

    public async Task<List<AppUser>> GetAllAgencies()
    {
        var snap = await Db.Collection("users").WhereEqualTo("Role", "agency").GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<AppUser>()).ToList();
    }

    public async Task DeleteAgency(string id)
    {
        await Db.Collection("portfolios").Document(id).DeleteAsync();
        await Db.Collection("users").Document(id).DeleteAsync();
    }

    public async Task<int> CountUsers(string role) =>
        (await Db.Collection("users").WhereEqualTo("Role", role).GetSnapshotAsync()).Count;

    // ---------- Portfolios ----------
    public async Task<Portfolio?> GetPortfolio(string agencyId)
    {
        var snap = await Db.Collection("portfolios").Document(agencyId).GetSnapshotAsync();
        return snap.Exists ? snap.ConvertTo<Portfolio>() : null;
    }

    public async Task SavePortfolio(Portfolio p) =>
        await Db.Collection("portfolios").Document(p.AgencyId).SetAsync(p);

    public async Task<List<Portfolio>> GetAllPortfolios()
    {
        var snap = await Db.Collection("portfolios").GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<Portfolio>()).ToList();
    }

    // ---------- Notifications ----------
    public async Task AddNotification(AppNotification n)
    {
        var doc = Db.Collection("notifications").Document();
        n.Id = doc.Id;
        await doc.SetAsync(n);
    }

    public async Task<List<AppNotification>> GetNotifications(int take = 40)
    {
        var snap = await Db.Collection("notifications")
            .OrderByDescending("CreatedAt").Limit(take).GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<AppNotification>()).ToList();
    }

    public async Task<int> CountUnread()
    {
        var snap = await Db.Collection("notifications")
            .WhereEqualTo("Read", false).Limit(99).GetSnapshotAsync();
        return snap.Count;
    }

    public async Task MarkAllRead()
    {
        var snap = await Db.Collection("notifications")
            .WhereEqualTo("Read", false).Limit(400).GetSnapshotAsync();
        if (snap.Count == 0) return;
        var batch = Db.StartBatch();
        foreach (var d in snap.Documents) batch.Update(d.Reference, "Read", true);
        await batch.CommitAsync();
    }

    // ---------- Meetings ----------
    public async Task CreateMeeting(Meeting m)
    {
        var doc = Db.Collection("meetings").Document();
        m.Id = doc.Id;
        await doc.SetAsync(m);
    }

    public async Task<List<Meeting>> GetMeetings()
    {
        var snap = await Db.Collection("meetings").OrderBy("StartsAt").GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<Meeting>()).ToList();
    }

    public async Task<List<Meeting>> GetMeetingsByBusiness(string businessId)
    {
        var snap = await Db.Collection("meetings")
            .WhereEqualTo("BusinessId", businessId).GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<Meeting>())
            .OrderByDescending(m => m.StartsAt).ToList();
    }

    public async Task<Meeting?> GetMeeting(string id)
    {
        var snap = await Db.Collection("meetings").Document(id).GetSnapshotAsync();
        return snap.Exists ? snap.ConvertTo<Meeting>() : null;
    }

    public async Task UpdateMeeting(Meeting m) =>
        await Db.Collection("meetings").Document(m.Id).SetAsync(m);

    // ---------- Chat ----------
    public async Task AddMessage(ChatMessage m)
    {
        var doc = Db.Collection("messages").Document();
        m.Id = doc.Id;
        await doc.SetAsync(m);
    }

    public async Task<List<ChatMessage>> GetMessages(string threadId, int take = 200)
    {
        var snap = await Db.Collection("messages").WhereEqualTo("ThreadId", threadId).GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<ChatMessage>())
            .OrderBy(m => m.CreatedAt).TakeLast(take).ToList();
    }

    public async Task<ChatThread?> GetThread(string id)
    {
        var snap = await Db.Collection("threads").Document(id).GetSnapshotAsync();
        return snap.Exists ? snap.ConvertTo<ChatThread>() : null;
    }

    public async Task SaveThread(ChatThread t) =>
        await Db.Collection("threads").Document(t.Id).SetAsync(t);

    public async Task<List<ChatThread>> GetThreads()
    {
        var snap = await Db.Collection("threads").OrderByDescending("LastAt").Limit(100).GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<ChatThread>()).ToList();
    }

    public async Task MarkThreadRead(string id, bool broker)
    {
        var docRef = Db.Collection("threads").Document(id);
        var snap = await docRef.GetSnapshotAsync();
        if (!snap.Exists) return;
        await docRef.UpdateAsync(broker ? "UnreadForBroker" : "UnreadForBusiness", 0);
    }

    // ---------- Reminders ----------
    public async Task<List<Meeting>> GetMeetingsBetween(DateTime fromUtc, DateTime toUtc)
    {
        var snap = await Db.Collection("meetings")
            .WhereGreaterThan("StartsAt", Timestamp.FromDateTime(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)))
            .WhereLessThan("StartsAt", Timestamp.FromDateTime(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)))
            .GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<Meeting>()).ToList();
    }

    public async Task SetReminderFlags(string id, bool day, bool hour) =>
        await Db.Collection("meetings").Document(id).UpdateAsync(new Dictionary<string, object>
        {
        { "ReminderDaySent", day },
        { "ReminderHourSent", hour }
        });

    // ---------- Business / Agency notifications ----------
    public async Task AddUserNotification(AppNotification n)
    {
        var doc = Db.Collection("user_notifications").Document();
        n.Id = doc.Id;
        await doc.SetAsync(n);
    }

    public async Task<List<AppNotification>> GetUserNotifications(string userId, int take = 50)
    {
        var snap = await Db.Collection("user_notifications").WhereEqualTo("UserId", userId).GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<AppNotification>())
            .OrderByDescending(n => n.CreatedAt).Take(take).ToList();
    }

    public async Task<int> CountUserUnread(string userId)
    {
        var snap = await Db.Collection("user_notifications")
            .WhereEqualTo("UserId", userId).WhereEqualTo("Read", false).Limit(99).GetSnapshotAsync();
        return snap.Count;
    }

    public async Task MarkUserRead(string userId)
    {
        var snap = await Db.Collection("user_notifications")
            .WhereEqualTo("UserId", userId).WhereEqualTo("Read", false).Limit(200).GetSnapshotAsync();
        if (snap.Count == 0) return;
        var batch = Db.StartBatch();
        foreach (var d in snap.Documents) batch.Update(d.Reference, "Read", true);
        await batch.CommitAsync();
    }
}