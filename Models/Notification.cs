using Google.Cloud.Firestore;
using System;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class AppNotification
    {
        public string Id { get; set; }

        [FirestoreProperty("title")]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty("description")]
        public string Description { get; set; } = string.Empty;

        [FirestoreProperty("type")]
        public string Type { get; set; } = "message"; 

        [FirestoreProperty("isUnread")]
        public bool IsUnread { get; set; } = true;

        [FirestoreProperty("timestamp")]
        public Timestamp Timestamp { get; set; } = Timestamp.GetCurrentTimestamp();

        public DateTime TimestampDateTime => Timestamp.ToDateTime();
    }
}
