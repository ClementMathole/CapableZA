using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Capableza.Web.Models
{
    [FirestoreData]
    public class AssignedTraining
    {
        public string? Id { get; set; }

        [FirestoreProperty("title")]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty("provider")]
        public string Provider { get; set; } = string.Empty;

        [FirestoreProperty("startDate")]
        public string StartDate { get; set; } = string.Empty; 

        [FirestoreProperty("endDate")]
        public string EndDate { get; set; } = string.Empty; 

        [FirestoreProperty("minimumParticipants")]
        public int MinimumParticipants { get; set; } = 0;

        [FirestoreProperty("status")]
        public string Status { get; set; } = "Upcoming";

        [FirestoreProperty("level")]
        public string Level { get; set; } = "Beginner"; 

        [FirestoreProperty("assignedTo")]
        public List<string> AssignedTo { get; set; } = new List<string>(); 

        [FirestoreProperty("createdBy")]
        public string CreatedBy { get; set; } = string.Empty; 

        public int EnrolledCount => AssignedTo?.Count ?? 0;

        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                ["title"] = Title ?? "",
                ["provider"] = Provider ?? "",
                ["startDate"] = StartDate ?? "",
                ["endDate"] = EndDate ?? "",
                ["level"] = Level ?? "",
                ["status"] = Status ?? "",
                ["assignedTo"] = AssignedTo ?? new List<string>(),
                ["createdBy"] = CreatedBy ?? ""
            };

            return dict;
        }

        public static AssignedTraining FromDictionary(IDictionary<string, object> data)
        {
            var t = new AssignedTraining();

            if (data == null) return t;

            object tmp;
            if (data.TryGetValue("title", out tmp)) t.Title = tmp?.ToString() ?? "";
            if (data.TryGetValue("provider", out tmp)) t.Provider = tmp?.ToString() ?? "";
            if (data.TryGetValue("startDate", out tmp)) t.StartDate = tmp?.ToString() ?? "";
            if (data.TryGetValue("endDate", out tmp)) t.EndDate = tmp?.ToString() ?? "";
            if (data.TryGetValue("level", out tmp)) t.Level = tmp?.ToString() ?? "";
            if (data.TryGetValue("status", out tmp)) t.Status = tmp?.ToString() ?? "";
            if (data.TryGetValue("createdBy", out tmp)) t.CreatedBy = tmp?.ToString() ?? "";

            if (data.TryGetValue("assignedTo", out tmp) && tmp != null)
            {
                if (tmp is IEnumerable<object> objEnum)
                    t.AssignedTo = objEnum.Select(x => x?.ToString() ?? "").ToList();
                else if (tmp is IEnumerable<string> strEnum)
                    t.AssignedTo = strEnum.ToList();
            }

            return t;
        }
    }
}
