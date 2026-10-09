using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class CalendarLink
{
    [FirestoreProperty] public string UserId { get; set; } = "";
    [FirestoreProperty] public string RefreshToken { get; set; } = "";   // متشفّر
    [FirestoreProperty] public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
}