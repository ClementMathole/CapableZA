using Google.Cloud.Firestore;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class AuditLog
    {
        public string Id { get; set; } 

        [FirestoreProperty("action")]
        public string Action { get; set; } = string.Empty; 

        [FirestoreProperty("timestamp")]
        public Timestamp Timestamp { get; set; } = Timestamp.GetCurrentTimestamp();

        [FirestoreProperty("actionTarget")]
        public string ActionTarget { get; set; } = string.Empty;

        [FirestoreProperty("performedBy")]
        public string PerformedBy { get; set; } = string.Empty; 

        public DateTime TimestampDateTime => Timestamp.ToDateTime();

        public string PerformedByName { get; set; } = string.Empty;
    }
}
