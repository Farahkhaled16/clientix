using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class Meeting
{
    [FirestoreProperty] public string Id { get; set; } = "";
    [FirestoreProperty] public string BusinessId { get; set; } = "";
    [FirestoreProperty] public string BusinessName { get; set; } = "";
    [FirestoreProperty] public string BusinessEmail { get; set; } = "";
    [FirestoreProperty] public string AgencyId { get; set; } = "";
    [FirestoreProperty] public string AgencyName { get; set; } = "";
    [FirestoreProperty] public string Note { get; set; } = "";
    [FirestoreProperty] public string Lang { get; set; } = "ar";          // لغة الـ Business وقت الحجز
    [FirestoreProperty] public DateTime StartsAt { get; set; }            // UTC
    [FirestoreProperty] public string Status { get; set; } = "pending";   // pending | approved | rejected | cancelled
    [FirestoreProperty] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [FirestoreProperty] public bool ReminderDaySent { get; set; }
    [FirestoreProperty] public bool ReminderHourSent { get; set; }
}