using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class PushToken
{
    [FirestoreProperty] public string Id { get; set; } = "";       // SHA256 للتوكن
    [FirestoreProperty] public string UserId { get; set; } = "";   // broker للـ Broker
    [FirestoreProperty] public string Token { get; set; } = "";
    [FirestoreProperty] public string Agent { get; set; } = "";
    [FirestoreProperty] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}