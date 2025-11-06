using Google.Cloud.Firestore;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class EmployeeTraining
    {
        public string Id { get; set; } 

        [FirestoreProperty("trainingName")]
        public string TrainingName { get; set; } = string.Empty;

        [FirestoreProperty("provider")]
        public string Provider { get; set; } = string.Empty;

        [FirestoreProperty("status")]
        public string Status { get; set; } = "planned"; 

        [FirestoreProperty("startDate")]
        public string StartDate { get; set; } = string.Empty;

        [FirestoreProperty("endDate")]
        public string EndDate { get; set; } = string.Empty;

        [FirestoreProperty("approved")]
        public bool Approved { get; set; } = false; 

        public string EmployeeId { get; set; } = string.Empty;
    }
}
