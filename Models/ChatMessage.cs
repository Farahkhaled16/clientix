using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class ChatMessage
{
    [FirestoreProperty] public string Id { get; set; } = "";
    [FirestoreProperty] public string ThreadId { get; set; } = "";
    [FirestoreProperty] public string SenderRole { get; set; } = "";    // business | broker
    [FirestoreProperty] public string SenderName { get; set; } = "";
    [FirestoreProperty] public string Text { get; set; } = "";
    [FirestoreProperty] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [FirestoreProperty] public string Kind { get; set; } = "text";      // text | image | video | file | agency
    [FirestoreProperty] public string MediaUrl { get; set; } = "";
    [FirestoreProperty] public string FileName { get; set; } = "";
    [FirestoreProperty] public string AgencyId { get; set; } = "";
}