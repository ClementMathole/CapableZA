using Google.Cloud.Firestore;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class UserRole
    {
        public string Uid { get; set; }

        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty("role")]
        public string Role { get; set; } = string.Empty;

        [FirestoreProperty("isFirstLogin")]
        public bool IsFirstLogin { get; set; } = true;

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("photoUrl")]
        public string PhotoUrl { get; set; } = string.Empty;
    }
}
