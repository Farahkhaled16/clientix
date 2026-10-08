using Google.Cloud.Firestore;

namespace BrokerHub.Models;

[FirestoreData]
public class WorkItem
{
    [FirestoreProperty] public string Title { get; set; } = "";
    [FirestoreProperty] public string Description { get; set; } = "";
    [FirestoreProperty] public string ImageUrl { get; set; } = "";   // لينك (القديم)
    [FirestoreProperty] public string Link { get; set; } = "";
    [FirestoreProperty] public string MediaUrl { get; set; } = "";   // ملف مرفوع
    [FirestoreProperty] public string MediaType { get; set; } = "";  // image | video | file

    public string Url => !string.IsNullOrEmpty(MediaUrl) ? MediaUrl : ImageUrl;
    public string Kind => !string.IsNullOrEmpty(MediaUrl) ? MediaType
                        : (string.IsNullOrEmpty(ImageUrl) ? "" : "image");
}

[FirestoreData]
public class Portfolio
{
    [FirestoreProperty] public string AgencyId { get; set; } = "";
    [FirestoreProperty] public string CompanyName { get; set; } = "";
    [FirestoreProperty] public string About { get; set; } = "";
    [FirestoreProperty] public string Services { get; set; } = "";   // مفصولة بفاصلة
    [FirestoreProperty] public string City { get; set; } = "";
    [FirestoreProperty] public int FoundedYear { get; set; }
    [FirestoreProperty] public int TeamSize { get; set; }
    [FirestoreProperty] public string Website { get; set; } = "";
    [FirestoreProperty] public string Instagram { get; set; } = "";
    [FirestoreProperty] public string LinkedIn { get; set; } = "";
    [FirestoreProperty] public string LogoUrl { get; set; } = "";
    [FirestoreProperty] public List<WorkItem> Works { get; set; } = new();


}