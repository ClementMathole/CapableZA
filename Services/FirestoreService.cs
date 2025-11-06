using Capableza.Web.Models;
using Firebase.Storage;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net; 
using System.Threading.Tasks;
using System.Web;

namespace Capableza.Web.Services
{
    public class FirestoreService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly StorageClient _storageClient;

        private const string EmployeesCollection = "employees";
        private const string SkillsSubCollection = "skills";
        private const string QualificationsSubCollection = "qualifications";
        private const string TrainingsSubCollection = "trainings";
        private const string AssignedTrainingsCollection = "assignedTrainings";
        private const string ReportsCollection = "reports";
        private const string NotificationsCollection = "notifications";
        private const string AuditLogsCollection = "auditLogs";
        private const string UsersCollection = "users";
        private const string ReportsBucketPath = "reports";
        private const string ProfilePicturesBucketPath = "profile_pictures";
        private const string QualificationsBucketPath = "qualifications";
        private const string SupportMessagesCollection = "supportMessages";

        private readonly string _storageBucketName = "skills-audit-db.firebasestorage.app";
        private readonly string _storageBucket = "skills-audit-db.appspot.com";


        public FirestoreService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
            _storageClient = StorageClient.Create();
        }

        public async Task AddUserRoleAsync(string uid, string email, string role)
        {
            if (string.IsNullOrEmpty(uid)) throw new ArgumentNullException(nameof(uid));

            DocumentReference userRef = _firestoreDb.Collection(UsersCollection).Document(uid);
            var userRoleData = new UserRole
            {
                Uid = uid,
                Email = email,
                Role = role,
                IsFirstLogin = true,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            };
            await userRef.SetAsync(userRoleData.ToDictionary());
        }

        public async Task AddInitialEmployeeProfileAsync(string uid, string email, string firstName, string lastName)
        {
            if (string.IsNullOrEmpty(uid)) throw new ArgumentNullException(nameof(uid));

            DocumentReference profileRef = _firestoreDb.Collection(EmployeesCollection).Document(uid);
            var profileForCalc = new EmployeeProfile
            {
                Uid = uid,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                IdNumber = "",
                JobTitle = ""
            };
            double initialCompletion = CalculateProfileCompletion(profileForCalc);
            var profileData = new Dictionary<string, object> {
                { "email", email },
                { "firstName", firstName },
                { "lastName", lastName },
                { "idNumber", "" },
                { "jobTitle", "" },
                { "profileCompletionStatus", initialCompletion },
                { "createdAt", Timestamp.GetCurrentTimestamp() },
                { "updatedAt", FieldValue.ServerTimestamp }
            };

            await profileRef.SetAsync(profileData);
            await LogActionAsync("AdminWebApp", "employeeAdded", $"Profile created for {uid} ({email})");
        }

        public async Task<EmployeeProfile?> GetEmployeeProfileAsync(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            DocumentReference docRef = _firestoreDb.Collection(EmployeesCollection).Document(uid);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            if (snapshot.Exists)
            {
                var profile = snapshot.ConvertTo<EmployeeProfile>();
                profile.Uid = snapshot.Id;
                return profile;
            }
            return null;
        }

        public async Task<List<EmployeeProfile>> GetAllEmployeeProfilesAsync()
        {
            QuerySnapshot snapshot = await _firestoreDb.Collection(EmployeesCollection).GetSnapshotAsync();
            return snapshot.Documents.Select(doc => {
                var profile = doc.ConvertTo<EmployeeProfile>();
                profile.Uid = doc.Id;
                return profile;
            }).ToList();
        }

        public async Task AddSupportMessageAsync(Dictionary<string, object> messageData)
        {
            if (messageData == null) throw new ArgumentNullException(nameof(messageData));
            await _firestoreDb.Collection("supportMessages").AddAsync(messageData);
        }

        public async Task UpdateEmployeeProfileAsync(
         string uid,
         EmployeeProfile profile,
         IFormFile? profilePictureFile = null,
         bool deletePicture = false)
            {
                var docRef = _firestoreDb.Collection(EmployeesCollection).Document(uid);
                var data = new Dictionary<string, object>
        {
            { "firstName", profile.FirstName?.Trim() ?? "" },
            { "lastName",  profile.LastName?.Trim()  ?? "" },
            { "idNumber",  profile.IdNumber?.Trim()  ?? "" },
            { "jobTitle",  profile.JobTitle?.Trim()  ?? "" },
            { "profilePicture", profile.ProfilePicture?.Trim() ?? "" },
            { "profileCompletionStatus", CalculateProfileCompletion(profile) },
            { "updatedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
        };
            try
            {
                if (profilePictureFile != null)
                {
                    string url = await UploadProfilePictureAsync(uid, profilePictureFile);
                    data["profilePicture"] = url;
                }
                else if (deletePicture)
                {
                    var snap = await docRef.GetSnapshotAsync();
                    if (snap.Exists && snap.ContainsField("profilePicture"))
                    {
                        var oldUrl = snap.GetValue<string>("profilePicture");
                        if (!string.IsNullOrEmpty(oldUrl))
                            await DeleteFromFirebaseStorageByUrlAsync(oldUrl);
                    }
                    data["profilePicture"] = FieldValue.Delete;
                }

                await docRef.SetAsync(data, SetOptions.MergeAll);
                await LogActionAsync(uid, "profileUpdated", "Profile details updated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FirestoreService] Error updating profile for {uid}: {ex.Message}");
                throw new Exception("Failed to update employee profile. Please try again later.", ex);
            }
        }


        public async Task<string> UploadProfilePictureAsync(string uid, IFormFile file)
        {
            if (string.IsNullOrEmpty(_storageBucketName))
                throw new InvalidOperationException("Firebase Storage Bucket Name not configured.");

            var fileExtension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{uid}_{Guid.NewGuid()}{fileExtension}";
            var objectName = $"{ProfilePicturesBucketPath}/{uid}/{uniqueFileName}";

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            try
            {
                await _storageClient.UploadObjectAsync(
                    _storageBucketName,
                    objectName,
                    file.ContentType,
                    memoryStream
                );

                string encodedPath = Uri.EscapeDataString(objectName);
                string token = Guid.NewGuid().ToString();
                string downloadUrl = $"https://firebasestorage.googleapis.com/v0/b/{_storageBucketName}/o/{encodedPath}?alt=media&token={token}";

                return downloadUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FirestoreService] Google Cloud Storage Upload failed for {uid}: {ex.Message}");
                throw new Exception("Failed to upload profile picture to Cloud Storage.", ex);
            }
        }

        public double CalculateProfileCompletion(EmployeeProfile profile)
        {
            int totalFields = 5;
            int filled = 0;
            if (!string.IsNullOrWhiteSpace(profile?.FirstName)) filled++;
            if (!string.IsNullOrWhiteSpace(profile?.LastName)) filled++;
            if (!string.IsNullOrWhiteSpace(profile?.Email)) filled++;
            if (!string.IsNullOrWhiteSpace(profile?.IdNumber)) filled++;
            if (!string.IsNullOrWhiteSpace(profile?.JobTitle)) filled++;
            return (filled / (double)totalFields) * 100.0;
        }

        public async Task DeleteFromFirebaseStorageByUrlAsync(string fileUrl)
        {
            string objectName = GetObjectNameFromUrl(fileUrl);
            if (string.IsNullOrEmpty(objectName))
            {
                Console.WriteLine($"Skipping Storage delete, could not parse object name from URL: {fileUrl}");
                return;
            }

            try
            {
                await _storageClient.DeleteObjectAsync(_storageBucketName, objectName);
                Console.WriteLine($"Successfully deleted {objectName} from Storage.");
            }
            catch (Google.GoogleApiException ex) when (ex.Error.Code == (int)HttpStatusCode.NotFound)
            {
                Console.WriteLine($"File not found in Storage, skipping delete: {objectName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARN: Error deleting file {objectName} from Storage: {ex.Message}");
            }
        }

        public async Task<string> UploadQualificationDocumentAsync(string uid, IFormFile file)
        {
            if (string.IsNullOrEmpty(_storageBucketName))
                throw new InvalidOperationException("Firebase Storage Bucket Name not configured.");

            var fileExtension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{uid}_{Guid.NewGuid()}{fileExtension}";
            var objectName = $"{QualificationsBucketPath}/{uid}/{uniqueFileName}";

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            try
            {
                await _storageClient.UploadObjectAsync(
                    _storageBucketName,
                    objectName,
                    file.ContentType,
                    memoryStream
                );

                return $"https://storage.googleapis.com/{_storageBucketName}/{objectName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FirestoreService] Google Cloud Storage Upload failed for {uid}: {ex.Message}");
                throw new Exception("Failed to upload qualification document to Cloud Storage.", ex);
            }
        }

        public async Task<(byte[] Bytes, string ContentType)> DownloadFileAsync(string fileUrl)
        {
            string objectName = GetObjectNameFromUrl(fileUrl);
            if (string.IsNullOrEmpty(objectName))
            {
                throw new Exception("Could not parse file path from URL.");
            }

            string contentType = Path.GetExtension(objectName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
            using (var memoryStream = new MemoryStream())
            {
                await _storageClient.DownloadObjectAsync(
                    _storageBucketName,
                    objectName,
                    memoryStream
                );

                return (memoryStream.ToArray(), contentType);
            }
        }

        public string GetObjectNameFromUrl(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl)) return string.Empty;
            try
            {
                Uri uri = new Uri(fileUrl);
                if (fileUrl.Contains("storage.googleapis.com"))
                {
                    string objectName = string.Join("", uri.Segments.Skip(2));
                    return Uri.UnescapeDataString(objectName);
                }
                else if (fileUrl.Contains("firebasestorage.googleapis.com"))
                {
                    string path = Uri.UnescapeDataString(uri.AbsolutePath);
                    string prefixToRemove = $"/v0/b/{_storageBucketName}/o/";
                    if (path.StartsWith(prefixToRemove))
                    {
                        return path.Substring(prefixToRemove.Length);
                    }
                }
                Console.WriteLine($"[FirestoreService] Could not parse object name from URL: {fileUrl}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FirestoreService] ERROR parsing object name from URL {fileUrl}: {ex.Message}");
                return string.Empty;
            }
        }



        public async Task<List<Skill>> GetEmployeeSkillsAsync(string employeeUid)
        {
            QuerySnapshot snapshot = await _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(SkillsSubCollection).GetSnapshotAsync();
            return snapshot.Documents.Select(doc => { var s = doc.ConvertTo<Skill>(); s.Id = doc.Id; return s; }).ToList();
        }
        public async Task DeleteEmployeeAsync(string uid, string adminUid)
        {
            if (string.IsNullOrEmpty(uid)) throw new ArgumentNullException(nameof(uid));

            DocumentReference employeeDocRef = _firestoreDb.Collection(EmployeesCollection).Document(uid);
            DocumentReference userDocRef = _firestoreDb.Collection(UsersCollection).Document(uid);

            await DeleteSubcollectionAsync(employeeDocRef.Collection(SkillsSubCollection));
            await DeleteSubcollectionAsync(employeeDocRef.Collection(QualificationsSubCollection));
            await DeleteSubcollectionAsync(employeeDocRef.Collection(TrainingsSubCollection));

            WriteBatch batch = _firestoreDb.StartBatch();
            batch.Delete(employeeDocRef); 
            batch.Delete(userDocRef);     
            await batch.CommitAsync();

            await LogActionAsync(adminUid, "employeeDeleted", $"Deleted employee {uid}");
        }

        private async Task DeleteSubcollectionAsync(CollectionReference collectionReference, int batchSize = 100)
        {
            QuerySnapshot snapshot = await collectionReference.Limit(batchSize).GetSnapshotAsync();
            while (snapshot.Documents.Count > 0)
            {
                WriteBatch batch = _firestoreDb.StartBatch();
                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    batch.Delete(document.Reference);
                }
                await batch.CommitAsync();

                snapshot = await collectionReference.Limit(batchSize).GetSnapshotAsync();
            }
        }
        public async Task AddSkillAsync(string employeeUid, Skill skill)
        {
            skill.CreatedAt = Timestamp.GetCurrentTimestamp();
            CollectionReference skillsRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(SkillsSubCollection);
            await skillsRef.AddAsync(skill.ToDictionary()); await LogActionAsync(employeeUid, "skillAdded", $"Skill '{skill.SkillName}'");
        }
        public async Task UpdateSkillAsync(string employeeUid, Skill skill)
        {
            if (string.IsNullOrEmpty(skill.Id)) throw new ArgumentException("Skill ID missing.");
            DocumentReference skillRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(SkillsSubCollection).Document(skill.Id);
            await skillRef.SetAsync(skill.ToDictionary(), SetOptions.MergeAll); await LogActionAsync(employeeUid, "skillUpdated", $"Skill '{skill.SkillName}' ID {skill.Id}");
        }
        public async Task DeleteSkillAsync(string employeeUid, string skillId)
        {
            DocumentReference skillRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(SkillsSubCollection).Document(skillId);
            await skillRef.DeleteAsync(); await LogActionAsync(employeeUid, "skillDeleted", $"Skill ID {skillId}");
        }
        public async Task<List<Qualification>> GetEmployeeQualificationsAsync(string employeeUid)
        {
            QuerySnapshot snapshot = await _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(QualificationsSubCollection).GetSnapshotAsync();
            return snapshot.Documents.Select(doc => { var q = doc.ConvertTo<Qualification>(); q.Id = doc.Id; q.EmployeeId = employeeUid; return q; }).ToList();
        }
        public async Task AddQualificationAsync(string employeeUid, Qualification qualification, IFormFile? documentFile)
        {
            qualification.IsVerified = false;
            qualification.IsRejected = false;

            if (documentFile != null)
            {
                string downloadUrl = await UploadQualificationDocumentAsync(employeeUid, documentFile);
                qualification.DocumentUrl = downloadUrl;
            }

            CollectionReference qualsRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(QualificationsSubCollection);
            await qualsRef.AddAsync(qualification.ToDictionary());
            await LogActionAsync(employeeUid, "qualificationAdded", $"Qual '{qualification.Title}'");
        }

        public async Task UpdateQualificationAsync(string employeeUid, Qualification qualification, IFormFile? documentFile)
        {
            if (string.IsNullOrEmpty(qualification.Id)) throw new ArgumentException("Qualification ID missing.");

            qualification.IsVerified = false;
            qualification.IsRejected = false;

            DocumentReference qualRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(QualificationsSubCollection).Document(qualification.Id);

            var snapshot = await qualRef.GetSnapshotAsync();
            string? oldDocumentUrl = null;
            if (snapshot.Exists && snapshot.ContainsField("documentUrl"))
            {
                oldDocumentUrl = snapshot.GetValue<string>("documentUrl");
            }

            if (documentFile != null)
            {
                if (!string.IsNullOrEmpty(oldDocumentUrl))
                {
                    await DeleteFromFirebaseStorageByUrlAsync(oldDocumentUrl);
                }
                string downloadUrl = await UploadQualificationDocumentAsync(employeeUid, documentFile);
                qualification.DocumentUrl = downloadUrl;
            }
            else
            {
                qualification.DocumentUrl = oldDocumentUrl;
            }

            await qualRef.SetAsync(qualification.ToDictionary(), SetOptions.MergeAll);
            await LogActionAsync(employeeUid, "qualificationUpdated", $"Qual '{qualification.Title}' ID {qualification.Id}");
        }
        public async Task UpdateQualificationAsync(string employeeUid, Qualification qualification)
        {
            if (string.IsNullOrEmpty(qualification.Id)) throw new ArgumentException("Qualification ID missing."); qualification.IsVerified = false; qualification.IsRejected = false; DocumentReference qualRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(QualificationsSubCollection).Document(qualification.Id);
            await qualRef.SetAsync(qualification.ToDictionary(), SetOptions.MergeAll); await LogActionAsync(employeeUid, "qualificationUpdated", $"Qual '{qualification.Title}' ID {qualification.Id}");
        }
        public async Task VerifyQualificationAsync(string employeeUid, string qualificationId)
        {
            DocumentReference qualRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(QualificationsSubCollection).Document(qualificationId); await qualRef.UpdateAsync(new Dictionary<string, object> { { "isVerified", true }, { "isRejected", false } }); await LogActionAsync("AdminWebApp", "qualificationVerified", $"Qual ID {qualificationId} for {employeeUid}");
        }
        public async Task RejectQualificationAsync(string employeeUid, string qualificationId)
        {
            DocumentReference qualRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(QualificationsSubCollection).Document(qualificationId); await qualRef.UpdateAsync(new Dictionary<string, object> { { "isVerified", false }, { "isRejected", true } }); await LogActionAsync("AdminWebApp", "qualificationRejected", $"Qual ID {qualificationId} for {employeeUid}");
        }
        public async Task DeleteQualificationAsync(string employeeUid, string qualificationId)
        {
            DocumentReference qualRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(QualificationsSubCollection).Document(qualificationId);

            try
            {
                var snapshot = await qualRef.GetSnapshotAsync();
                if (snapshot.Exists && snapshot.ContainsField("documentUrl"))
                {
                    var fileUrl = snapshot.GetValue<string>("documentUrl");
                    if (!string.IsNullOrEmpty(fileUrl))
                    {
                        await DeleteFromFirebaseStorageByUrlAsync(fileUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARN: Could not delete qualification file from Storage: {ex.Message}");
            }

            await qualRef.DeleteAsync();
            await LogActionAsync(employeeUid, "qualificationDeleted", $"Qual ID {qualificationId}");
        }
        public async Task<List<EmployeeTraining>> GetEmployeeTrainingsAsync(string employeeUid)
        {
            QuerySnapshot snapshot = await _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(TrainingsSubCollection).GetSnapshotAsync(); return snapshot.Documents.Select(doc => { var t = doc.ConvertTo<EmployeeTraining>(); t.Id = doc.Id; t.EmployeeId = employeeUid; return t; }).ToList();
        }
        public async Task AddEmployeeTrainingAsync(string employeeUid, EmployeeTraining training)
        {
            training.Approved = false; training.Status = "planned"; CollectionReference trainingsRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(TrainingsSubCollection); await trainingsRef.AddAsync(training.ToDictionary()); await LogActionAsync(employeeUid, "trainingAdded", $"Training '{training.TrainingName}'"); await CreateAdminNotification("Training Approval Required", $"'{training.TrainingName}' submitted by employee {employeeUid} needs approval.");
        }
        public async Task UpdateEmployeeTrainingAsync(string employeeUid, EmployeeTraining training)
        {
            if (string.IsNullOrEmpty(training.Id)) throw new ArgumentException("Training ID missing."); training.Approved = false; training.Status = "planned"; DocumentReference trainingRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(TrainingsSubCollection).Document(training.Id); await trainingRef.SetAsync(training.ToDictionary(), SetOptions.MergeAll); await LogActionAsync(employeeUid, "trainingUpdated", $"Training '{training.TrainingName}' ID {training.Id}"); await CreateAdminNotification("Training Update Requires Approval", $"'{training.TrainingName}' updated by employee {employeeUid} needs re-approval.");
        }
        public async Task DeleteEmployeeTrainingAsync(string employeeUid, string trainingId)
        {
            DocumentReference trainingRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(TrainingsSubCollection).Document(trainingId); await trainingRef.DeleteAsync(); await LogActionAsync(employeeUid, "trainingDeleted", $"Training ID {trainingId}");
        }
        public async Task ApproveEmployeeTrainingAsync(string employeeUid, string trainingId)
        {
            DocumentReference trainingRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(TrainingsSubCollection).Document(trainingId); await trainingRef.UpdateAsync(new Dictionary<string, object> { { "approved", true }, { "status", "in-progress" } }); await LogActionAsync("AdminWebApp", "trainingApproved", $"Training ID {trainingId} for {employeeUid}"); await CreateUserNotification(employeeUid, "Training Approved", $"Your submitted training (ID: {trainingId}) has been approved.");
        }
        public async Task RejectEmployeeTrainingAsync(string employeeUid, string trainingId)
        {
            DocumentReference trainingRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(TrainingsSubCollection).Document(trainingId); await trainingRef.UpdateAsync(new Dictionary<string, object> { { "approved", false }, { "status", "rejected" } }); await LogActionAsync("AdminWebApp", "trainingRejected", $"Training ID {trainingId} for {employeeUid}"); await CreateUserNotification(employeeUid, "Training Rejected", $"Your submitted training (ID: {trainingId}) has been rejected.");
        }
        public async Task<List<AssignedTraining>> GetAllAssignedTrainingsAsync()
        {
            Query query = _firestoreDb.Collection(AssignedTrainingsCollection).OrderByDescending("startDate"); QuerySnapshot snapshot = await query.GetSnapshotAsync(); return snapshot.Documents.Select(doc => { var t = doc.ConvertTo<AssignedTraining>(); t.Id = doc.Id; return t; }).ToList();
        }
        public async Task AssignTrainingAsync(AssignedTraining training, string adminUid)
        {
            training.CreatedBy = adminUid; CollectionReference trainingsRef = _firestoreDb.Collection(AssignedTrainingsCollection); DocumentReference addedDocRef = await trainingsRef.AddAsync(training.ToDictionary()); await LogActionAsync(adminUid, "trainingAssigned", $"Training '{training.Title}' assigned to {training.AssignedTo.Count} users"); foreach (var empId in training.AssignedTo) { await CreateUserNotification(empId, "New Training Assigned", $"You have been assigned to the training: '{training.Title}'."); }
        }
        public async Task UpdateAssignedTrainingAsync(AssignedTraining training, string adminUid)
        {
            if (string.IsNullOrEmpty(training.Id)) throw new ArgumentException("Assigned Training ID missing."); DocumentReference trainingRef = _firestoreDb.Collection(AssignedTrainingsCollection).Document(training.Id); await trainingRef.SetAsync(training.ToDictionary(), SetOptions.MergeAll); await LogActionAsync(adminUid, "trainingAssignmentUpdated", $"Training '{training.Title}' ID {training.Id}");
        }
        public async Task DeleteAssignedTrainingAsync(string trainingId, string adminUid)
        {
            DocumentReference trainingRef = _firestoreDb.Collection(AssignedTrainingsCollection).Document(trainingId); await trainingRef.DeleteAsync(); await LogActionAsync(adminUid, "trainingAssignmentDeleted", $"Training ID {trainingId}");
        }

        public async Task<List<Report>> GetAllReportsAsync()
        {
            Query query = _firestoreDb.Collection(ReportsCollection).OrderByDescending("createdAt");
            QuerySnapshot snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => { var r = doc.ConvertTo<Report>(); r.Id = doc.Id; return r; }).ToList();
        }

        public async Task<Report?> GetReportAsync(string reportId)
        {
            if (string.IsNullOrEmpty(reportId)) return null;
            DocumentReference docRef = _firestoreDb.Collection(ReportsCollection).Document(reportId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            if (snapshot.Exists)
            {
                var report = snapshot.ConvertTo<Report>();
                report.Id = snapshot.Id;
                return report;
            }
            return null;
        }

        public async Task<string> UploadReportToStorageAsync(byte[] pdfBytes, string fileName, string userId)
        {
            if (string.IsNullOrEmpty(_storageBucketName) || _storageBucketName.Contains("YOUR_PROJECT_ID"))
            {
                throw new InvalidOperationException("Firebase Storage Bucket Name is not configured.");
            }
            var storage = new FirebaseStorage(_storageBucketName);
            var storagePath = $"{ReportsBucketPath}/{userId}/{fileName}";

            using (var memoryStream = new MemoryStream(pdfBytes))
            {
                var downloadUrl = await storage
                    .Child(storagePath)
                    .PutAsync(memoryStream);
                return downloadUrl;
            }
        }


        public async Task<string> UploadProfilePictureToStorageAsync(byte[] profileBytes, string profileName, string userId)
        {
            if (string.IsNullOrEmpty(_storageBucketName))
                throw new InvalidOperationException("Firebase Storage Bucket Name is not configured.");

            var storage = new FirebaseStorage(_storageBucketName);

            using (var memoryStream = new MemoryStream(profileBytes))
            {
                var downloadUrl = await storage
                    .Child(ProfilePicturesBucketPath)
                    .Child(userId)
                    .Child(profileName)
                    .PutAsync(memoryStream);

                return downloadUrl;
            }
        }


        public async Task AddReportAsync(Report report)
        {
            report.CreatedAt = Timestamp.GetCurrentTimestamp();
            await _firestoreDb.Collection(ReportsCollection).AddAsync(report.ToDictionary());
            await LogActionAsync(report.GeneratedBy, "reportGenerated", $"Type: {report.ReportType}");
        }

        public async Task DeleteReportAsync(string reportId, string adminUid)
        {
            if (string.IsNullOrEmpty(reportId)) throw new ArgumentNullException(nameof(reportId));
            DocumentReference reportRef = _firestoreDb.Collection(ReportsCollection).Document(reportId);
            try
            {
                var reportDoc = await reportRef.GetSnapshotAsync();
                if (reportDoc.Exists)
                {
                    var report = reportDoc.ConvertTo<Report>();
                    if (!string.IsNullOrEmpty(report.FileUrl))
                    {
                        await DeleteFromFirebaseStorageByUrlAsync(report.FileUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARN: Could not delete report file from Storage: {ex.Message}");
            }
            await reportRef.DeleteAsync();
            await LogActionAsync(adminUid, "reportDeleted", $"Report ID {reportId}");
        }

        public async Task<Skill?> GetSkillAsync(string employeeUid, string skillId)
        {
            if (string.IsNullOrEmpty(employeeUid) || string.IsNullOrEmpty(skillId)) return null;
            DocumentReference docRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(SkillsSubCollection).Document(skillId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            if (snapshot.Exists) { var s = snapshot.ConvertTo<Skill>(); s.Id = snapshot.Id; return s; }
            return null;
        }

        public async Task<Qualification?> GetQualificationAsync(string employeeUid, string qualificationId)
        {
            if (string.IsNullOrEmpty(employeeUid) || string.IsNullOrEmpty(qualificationId)) return null;
            DocumentReference docRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(QualificationsSubCollection).Document(qualificationId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            if (snapshot.Exists)
            {
                var q = snapshot.ConvertTo<Qualification>();
                q.Id = snapshot.Id;
                q.EmployeeId = employeeUid;
                return q;
            }
            return null;
        }

        public async Task<EmployeeTraining?> GetEmployeeTrainingAsync(string employeeUid, string trainingId)
        {
            if (string.IsNullOrEmpty(employeeUid) || string.IsNullOrEmpty(trainingId)) return null;
            DocumentReference docRef = _firestoreDb.Collection(EmployeesCollection).Document(employeeUid).Collection(TrainingsSubCollection).Document(trainingId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            if (snapshot.Exists)
            {
                var t = snapshot.ConvertTo<EmployeeTraining>();
                t.Id = snapshot.Id;
                t.EmployeeId = employeeUid;
                return t;
            }
            return null;
        }

        public async Task<AssignedTraining?> GetAssignedTrainingByIdAsync(string id)
        {
            var docRef = _firestoreDb.Collection(AssignedTrainingsCollection).Document(id);
            var snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
                return null;

            var data = snapshot.ToDictionary();
            var training = AssignedTraining.FromDictionary(data);
            training.Id = snapshot.Id;

            return training;
        }


        public async Task<List<AppNotification>> GetAllNotificationsAsync()
        {
            Query query = _firestoreDb.Collection(NotificationsCollection).OrderByDescending("timestamp").Limit(100); QuerySnapshot snapshot = await query.GetSnapshotAsync(); return snapshot.Documents.Select(doc => { var n = doc.ConvertTo<AppNotification>(); n.Id = doc.Id; return n; }).ToList();
        }
        public async Task MarkNotificationReadAsync(string notificationId)
        {
            DocumentReference notifRef = _firestoreDb.Collection(NotificationsCollection).Document(notificationId); await notifRef.UpdateAsync("isUnread", false);
        }
        public async Task MarkAllNotificationsReadAsync()
        {
            Query query = _firestoreDb.Collection(NotificationsCollection).WhereEqualTo("isUnread", true); QuerySnapshot snapshot = await query.GetSnapshotAsync(); WriteBatch batch = _firestoreDb.StartBatch(); foreach (var doc in snapshot.Documents) { batch.Update(doc.Reference, "isUnread", false); }
            await batch.CommitAsync(); await LogActionAsync("AdminWebApp", "notificationsReadAll", $"Marked {snapshot.Count} notifications read");
        }
        public async Task CreateAdminNotification(string title, string description)
        {
            var notification = new AppNotification { Title = title, Description = description, Type = "alert", IsUnread = true, Timestamp = Timestamp.GetCurrentTimestamp() }; await _firestoreDb.Collection(NotificationsCollection).AddAsync(notification.ToDictionary());
        }
        public async Task CreateUserNotification(string userId, string title, string description)
        {
            var notification = new AppNotification { Title = title, Description = description, Type = "message", IsUnread = true, Timestamp = Timestamp.GetCurrentTimestamp() }; await _firestoreDb.Collection(NotificationsCollection).AddAsync(notification.ToDictionary());
        }
        public async Task<List<AuditLog>> GetRecentAuditLogsAsync(int limit = 20)
        {
            Query query = _firestoreDb.Collection(AuditLogsCollection).OrderByDescending("timestamp").Limit(limit); QuerySnapshot snapshot = await query.GetSnapshotAsync(); var logs = new List<AuditLog>(); var employeeProfiles = await GetAllEmployeeProfilesAsync(); var profileMap = employeeProfiles.ToDictionary(p => p.Uid, p => p.FullName); foreach (var doc in snapshot.Documents) { var log = doc.ConvertTo<AuditLog>(); log.Id = doc.Id; log.PerformedByName = profileMap.TryGetValue(log.PerformedBy, out var name) ? name : log.PerformedBy; logs.Add(log); }
            return logs;
        }
        public async Task LogActionAsync(string userId, string action, string target)
        {
            if (string.IsNullOrEmpty(userId)) userId = "WebAppSystem"; var logEntry = new Dictionary<string, object> { { "performedBy", userId }, { "action", action }, { "actionTarget", target }, { "timestamp", Timestamp.GetCurrentTimestamp() } }; await _firestoreDb.Collection(AuditLogsCollection).AddAsync(logEntry);
        }

    }

    public static class FirestoreModelExtensions
    {
        public static Dictionary<string, object> ToDictionary(this UserRole u) { return new Dictionary<string, object> { { "email", u.Email }, { "role", u.Role }, { "isFirstLogin", u.IsFirstLogin }, { "createdAt", u.CreatedAt } }; }
        public static Dictionary<string, object> ToDictionary(this Skill s) { return new Dictionary<string, object> { { "skillName", s.SkillName }, { "category", s.Category }, { "proficiency", s.Proficiency }, { "dateAcquired", s.DateAcquired }, { "createdAt", s.CreatedAt } }; }
        public static Dictionary<string, object> ToDictionary(this Qualification q) { return new Dictionary<string, object> { { "title", q.Title }, { "institute", q.Institute }, { "yearObtained", q.YearObtained }, { "type", q.Type }, { "serialNumber", q.SerialNumber }, { "isVerified", q.IsVerified }, { "isRejected", q.IsRejected }, { "documentUrl", q.DocumentUrl } }; }
        public static Dictionary<string, object> ToDictionary(this EmployeeTraining t) { return new Dictionary<string, object> { { "trainingName", t.TrainingName }, { "provider", t.Provider }, { "status", t.Status }, { "startDate", t.StartDate }, { "endDate", t.EndDate }, { "approved", t.Approved } }; }
        public static Dictionary<string, object> ToDictionary(this AssignedTraining t) { return new Dictionary<string, object> { { "title", t.Title }, { "provider", t.Provider }, { "startDate", t.StartDate }, { "endDate", t.EndDate }, { "minimumParticipants", t.MinimumParticipants }, { "status", t.Status }, { "level", t.Level }, { "assignedTo", t.AssignedTo }, { "createdBy", t.CreatedBy } }; }
        public static Dictionary<string, object> ToDictionary(this Report r) { return new Dictionary<string, object> { { "reportType", r.ReportType }, { "positionOrRole", r.PositionOrRole }, { "dateRange", r.DateRange }, { "includeVisualizations", r.IncludeVisualizations }, { "generatedBy", r.GeneratedBy }, { "createdAt", r.CreatedAt }, { "fileUrl", r.FileUrl } }; }
        public static Dictionary<string, object> ToDictionary(this AppNotification n) { return new Dictionary<string, object> { { "title", n.Title }, { "description", n.Description }, { "type", n.Type }, { "isUnread", n.IsUnread }, { "timestamp", n.Timestamp } }; }
    }
}