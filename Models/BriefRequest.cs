using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class BriefRequest
{
    [FirestoreProperty] public string Id { get; set; } = "";
    [FirestoreProperty] public string BusinessId { get; set; } = "";
    [FirestoreProperty] public string BusinessName { get; set; } = "";
    [FirestoreProperty] public string BusinessEmail { get; set; } = "";
    [FirestoreProperty] public string Lang { get; set; } = "ar";
    [FirestoreProperty] public string Services { get; set; } = "";    // مفاتيح مفصولة بفاصلة
    [FirestoreProperty] public string Budget { get; set; } = "";
    [FirestoreProperty] public string Timeline { get; set; } = "";
    [FirestoreProperty] public string City { get; set; } = "";
    [FirestoreProperty] public string Details { get; set; } = "";
    [FirestoreProperty] public string Status { get; set; } = "new";   // new | matched | closed
    [FirestoreProperty] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [FirestoreProperty] public List<string> Recommended { get; set; } = new();
    [FirestoreProperty] public string BrokerNote { get; set; } = "";
    [FirestoreProperty] public DateTime? MatchedAt { get; set; }
}