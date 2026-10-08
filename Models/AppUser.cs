using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class AppUser
{
    [FirestoreProperty] public string Id { get; set; } = "";
    [FirestoreProperty] public string Name { get; set; } = "";
    [FirestoreProperty] public string Email { get; set; } = "";
    [FirestoreProperty] public string PasswordHash { get; set; } = "";
    [FirestoreProperty] public string Role { get; set; } = "business"; // business | agency
    [FirestoreProperty] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty] public bool EmailConfirmed { get; set; }
    [FirestoreProperty] public string ConfirmToken { get; set; } = "";
    [FirestoreProperty] public string ResetToken { get; set; } = "";
    [FirestoreProperty] public DateTime? ResetExpires { get; set; }
    [FirestoreProperty] public string Provider { get; set; } = "local"; // local | google
}