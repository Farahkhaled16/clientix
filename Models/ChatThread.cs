using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class ChatThread
{
    [FirestoreProperty] public string Id { get; set; } = "";
    [FirestoreProperty] public string BusinessName { get; set; } = "";
    [FirestoreProperty] public string LastText { get; set; } = "";
    [FirestoreProperty] public DateTime LastAt { get; set; } = DateTime.UtcNow;
    [FirestoreProperty] public int UnreadForBroker { get; set; }
    [FirestoreProperty] public int UnreadForBusiness { get; set; }
    [FirestoreProperty] public DateTime? BusinessReadAt { get; set; }
    [FirestoreProperty] public DateTime? BrokerReadAt { get; set; }
}