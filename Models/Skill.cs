using Google.Cloud.Firestore;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class Skill
    {
        public string Id { get; set; }

        [FirestoreProperty("skillName")]
        public string SkillName { get; set; } = string.Empty;

        [FirestoreProperty("category")]
        public string Category { get; set; } = string.Empty;

        [FirestoreProperty("proficiency")]
        public int Proficiency { get; set; } = 0;

        [FirestoreProperty("dateAcquired")]
        public string DateAcquired { get; set; } = string.Empty;

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();

        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
    }
}
