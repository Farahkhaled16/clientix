using Google.Cloud.Firestore;
using BrokerHub.Models;

namespace BrokerHub.Services;

public partial class FirestoreService
{
    // ---------- Chat read receipts ----------
    public async Task MarkThreadSeen(string id, bool broker)
    {
        var docRef = Db.Collection("threads").Document(id);
        var snap = await docRef.GetSnapshotAsync();
        if (!snap.Exists) return;
        await docRef.UpdateAsync(new Dictionary<string, object>
        {
            { broker ? "UnreadForBroker" : "UnreadForBusiness", 0 },
            { broker ? "BrokerReadAt" : "BusinessReadAt", Timestamp.GetCurrentTimestamp() }
        });
    }

    // ---------- Brief requests ----------
    public async Task CreateRequest(BriefRequest r)
    {
        var doc = Db.Collection("requests").Document();
        r.Id = doc.Id;
        await doc.SetAsync(r);
    }

    public async Task<BriefRequest?> GetRequest(string id)
    {
        var snap = await Db.Collection("requests").Document(id).GetSnapshotAsync();
        return snap.Exists ? snap.ConvertTo<BriefRequest>() : null;
    }

    public async Task<List<BriefRequest>> GetRequests()
    {
        var snap = await Db.Collection("requests").GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<BriefRequest>())
            .OrderBy(r => r.Status == "new" ? 0 : r.Status == "matched" ? 1 : 2)
            .ThenByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<List<BriefRequest>> GetRequestsByBusiness(string businessId)
    {
        var snap = await Db.Collection("requests").WhereEqualTo("BusinessId", businessId).GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<BriefRequest>())
            .OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task UpdateRequest(BriefRequest r) =>
        await Db.Collection("requests").Document(r.Id).SetAsync(r);

    public async Task<int> CountNewRequests()
    {
        var snap = await Db.Collection("requests").WhereEqualTo("Status", "new").Limit(99).GetSnapshotAsync();
        return snap.Count;
    }
}