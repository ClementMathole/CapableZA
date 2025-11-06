using Google.Cloud.Firestore;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class Qualification
    {
        public string Id { get; set; } 

        [FirestoreProperty("title")]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty("institute")]
        public string Institute { get; set; } = string.Empty;

        [FirestoreProperty("yearObtained")]
        public string YearObtained { get; set; } = string.Empty; 

        [FirestoreProperty("type")]
        public string Type { get; set; } = string.Empty;

        [FirestoreProperty("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [FirestoreProperty("isVerified")]
        public bool IsVerified { get; set; } = false;

        [FirestoreProperty("isRejected")]
        public bool IsRejected { get; set; } = false;

        [FirestoreProperty("documentUrl")]
        public string? DocumentUrl { get; set; }

        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string? ProfilePicture { get; set; }

        public string Status => IsVerified ? "Verified" : (IsRejected ? "Rejected" : "Pending");
    }
}
