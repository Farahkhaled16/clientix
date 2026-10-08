using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class AppNotification
{
    [FirestoreProperty] public string Id { get; set; } = "";
    [FirestoreProperty] public string UserId { get; set; } = "";   // فاضي = إشعار الـ Broker
    [FirestoreProperty] public string Type { get; set; } = "";
    [FirestoreProperty] public string Body { get; set; } = "";
    [FirestoreProperty] public string Link { get; set; } = "";
    [FirestoreProperty] public bool Read { get; set; }
    [FirestoreProperty] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}