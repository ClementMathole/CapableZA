using Google.Cloud.Firestore;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class EmployeeProfile
    {
        public string? Uid { get; set; } 
        [FirestoreProperty("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [FirestoreProperty("lastName")]
        public string LastName { get; set; } = string.Empty;

        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty("idNumber")]
        public string IdNumber { get; set; } = string.Empty;

        [FirestoreProperty("jobTitle")]
        public string JobTitle { get; set; } = string.Empty;

        [FirestoreProperty("profilePicture")]
        public string? ProfilePicture { get; set; }

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();

        [FirestoreProperty("updatedAt")]
        public Timestamp? UpdatedAt { get; set; }

        [FirestoreProperty("profileCompletionStatus")] 
        public double ProfileCompletionStatus { get; set; } = 0.0;

        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
