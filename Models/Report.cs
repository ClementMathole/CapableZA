using Google.Cloud.Firestore;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class Report
    {
        public string? Id { get; set; } 

        [FirestoreProperty("reportType")]
        public string ReportType { get; set; } = string.Empty;

        [FirestoreProperty("positionOrRole")]
        public string PositionOrRole { get; set; } = string.Empty;

        [FirestoreProperty("dateRange")]
        public string DateRange { get; set; } = string.Empty;

        [FirestoreProperty("includeVisualizations")]
        public bool IncludeVisualizations { get; set; } = false;

        [FirestoreProperty("generatedBy")]
        public string GeneratedBy { get; set; } = string.Empty; 

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();

        [FirestoreProperty("fileUrl")]
        public string FileUrl { get; set; } = string.Empty; 

        public DateTime CreatedAtDateTime => CreatedAt.ToDateTime();
    }
}
