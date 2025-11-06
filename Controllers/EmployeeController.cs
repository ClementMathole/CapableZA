using Capableza.Web.Models;
using Capableza.Web.ViewModels;
using Capableza.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Capableza.Web.Controllers
{
    [Authorize(Roles = "employee")]
    public class EmployeeController : Controller
    {
        private readonly FirestoreService _firestoreService;
        private readonly AuthService _authService;
        private readonly IHttpContextAccessor _http;

        public EmployeeController(FirestoreService firestoreService, AuthService authService, IHttpContextAccessor http)
        {
            _firestoreService = firestoreService;
            _authService = authService;
            _http = http;
        }

        private string GetCurrentUserUid() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("User not authenticated.");

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                string userUid = GetCurrentUserUid();
                var profile = await _firestoreService.GetEmployeeProfileAsync(userUid);
                if (profile == null) return NotFound("Employee profile not found.");
                var assignedTrainings = await _firestoreService.GetAllAssignedTrainingsAsync();
                var userAssigned = assignedTrainings
                                    .Where(t => t.AssignedTo.Contains(userUid) && t.Status.Equals("Upcoming", StringComparison.OrdinalIgnoreCase))
                                    .OrderBy(t => DateTime.TryParse(t.StartDate, out var date) ? date : DateTime.MaxValue)
                                    .FirstOrDefault();
                ViewBag.UpcomingTraining = userAssigned;
                var recentActivities = await _firestoreService.GetRecentAuditLogsAsync(3);
                var userRecentActivities = recentActivities.Where(log => log.PerformedBy == userUid).ToList();
                ViewBag.RecentActivities = userRecentActivities;
                ViewBag.IsAdminViewing = false;
                return View(profile);
            }
            catch (UnauthorizedAccessException)
            {
                return Challenge();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading employee dashboard: {ex}");
                TempData["ErrorMessage"] = "Error loading dashboard data. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSupportMessage(string supportMessage)
        {
            string userUid = "";
            try
            {
                userUid = GetCurrentUserUid();
                if (string.IsNullOrWhiteSpace(supportMessage))
                {
                    TempData["ErrorMessage"] = "Support message cannot be empty.";
                    return RedirectToAction("Dashboard");
                }

                var messageData = new Dictionary<string, object> {
                     { "userId", userUid },
                     { "message", supportMessage.Trim() },
                     { "timestamp", Timestamp.GetCurrentTimestamp() },
                     { "status", "New" }
                 };
                await _firestoreService.AddSupportMessageAsync(messageData);

                await _firestoreService.LogActionAsync(userUid, "supportMessageSent", "Support message sent via web");

                TempData["SuccessMessage"] = "Your message has been sent to the admin.";
            }
            catch (UnauthorizedAccessException)
            {
                return Challenge();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending support message for {userUid}: {ex}");
                TempData["ErrorMessage"] = $"Failed to send message: {ex.Message}";
            }
            return RedirectToAction("Dashboard");
        }

        #region Other IActionResults
        [HttpGet]
        public async Task<IActionResult> Profile(string? id = null)
        {
            try
            {
                string targetUid = GetCurrentUserUid();
                var profile = await _firestoreService.GetEmployeeProfileAsync(targetUid);

                if (profile == null)
                    return NotFound();

                ViewBag.IsAdminViewing = false;
                return View(profile);
            }
            catch (UnauthorizedAccessException)
            {
                return Challenge();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(EmployeeProfile model,
                                                IFormFile? profilePictureFile,
                                                bool deleteProfilePicture = false)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Login", "Account");

            model.Uid = uid;
            if (string.IsNullOrWhiteSpace(model.FirstName))
                ModelState.AddModelError("FirstName", "First name is required.");
            if (string.IsNullOrWhiteSpace(model.LastName))
                ModelState.AddModelError("LastName", "Last name is required.");

            if (profilePictureFile != null && profilePictureFile.Length > 2 * 1024 * 1024)
                ModelState.AddModelError("profilePictureFile", "Picture must be ≤ 2 MB.");

            if (!ModelState.IsValid)
            {
                var freshProfile = await _firestoreService.GetEmployeeProfileAsync(uid);
                if (freshProfile != null)
                {
                    model.Uid = uid;
                    freshProfile.FirstName = model.FirstName;
                    freshProfile.LastName = model.LastName;
                    freshProfile.IdNumber = model.IdNumber;
                    freshProfile.JobTitle = model.JobTitle;
                    return View(freshProfile);
                }
                return View(model);
            }

            try
            {
                await _firestoreService.UpdateEmployeeProfileAsync(uid, model, profilePictureFile, deleteProfilePicture);
                var updatedProfile = await _firestoreService.GetEmployeeProfileAsync(uid);

                TempData["SuccessMessage"] = "Profile updated successfully!";
                ViewBag.IsAdminViewing = false;

                return View(updatedProfile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profile update failed for {uid}: {ex}");
                TempData["ErrorMessage"] = $"Failed to save profile: {ex.Message}";

                var errorProfile = await _firestoreService.GetEmployeeProfileAsync(uid);
                if (errorProfile != null)
                {
                    model.Uid = uid;
                    errorProfile.FirstName = model.FirstName;
                    errorProfile.LastName = model.LastName;
                    errorProfile.IdNumber = model.IdNumber;
                    errorProfile.JobTitle = model.JobTitle;
                    return View(errorProfile);
                }

                return View(model);
            }
        }
        public async Task<IActionResult> Skills(string? id = null) {
            try {
                string targetUid = GetCurrentUserUid();
                var skills = await _firestoreService.GetEmployeeSkillsAsync(targetUid);
                ViewBag.EmployeeUid = targetUid; ViewBag.IsAdminViewing = false;
                return View(skills);
            } catch (UnauthorizedAccessException) {
                return Challenge();
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View("Error");
            }
        }

        public async Task<IActionResult> Qualifications(string? id = null) {
            try {
                string targetUid = GetCurrentUserUid();
                var qualifications = await _firestoreService.GetEmployeeQualificationsAsync(targetUid);
                ViewBag.EmployeeUid = targetUid;
                ViewBag.IsAdminViewing = false;
                return View(qualifications);
            } catch (UnauthorizedAccessException) {
                return Challenge();
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View("Error");
            }
        }

        public async Task<IActionResult> Trainings(string? id = null) {
            try {
                string targetUid = GetCurrentUserUid();
                var trainings = await _firestoreService.GetEmployeeTrainingsAsync(targetUid);
                ViewBag.EmployeeUid = targetUid;
                ViewBag.IsAdminViewing = false;
                return View(trainings);
            } catch (UnauthorizedAccessException) {
                return Challenge();
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> AddSkill([FromBody] Skill skill) {
            try {
                string userUid = GetCurrentUserUid();
                await _firestoreService.AddSkillAsync(userUid, skill);
                return Ok(new { success = true, message = "Skill added." });
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            } catch (Exception ex) {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSkill([FromBody] Skill skill) {
            try {
                string userUid = GetCurrentUserUid();

                if (string.IsNullOrEmpty(skill.Id))
                    return BadRequest("ID required.");
                await _firestoreService.UpdateSkillAsync(userUid, skill);
                
                return Ok(new { success = true, message = "Skill updated." });
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            } catch (Exception ex) {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> DeleteSkill(string id) {
            try {
                string userUid = GetCurrentUserUid();

                if (string.IsNullOrEmpty(id))
                    return BadRequest("ID required.");
                await _firestoreService.DeleteSkillAsync(userUid, id);
                return Ok(new { success = true, message = "Skill deleted." });
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            } catch (Exception ex) {
                return StatusCode(500, new { success = false, message = ex.Message });
            } 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQualification([FromForm] Qualification qualification, IFormFile? documentFile)
        {
            try {
                string userUid = GetCurrentUserUid();
                await _firestoreService.AddQualificationAsync(userUid, qualification, documentFile);
                return Ok(new { success = true, message = "Qualification added." });
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            } catch (Exception ex) {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQualification([FromForm] Qualification qualification, IFormFile? documentFile)
        {
            try
            {
                string userUid = GetCurrentUserUid();
                if (string.IsNullOrEmpty(qualification.Id)) return BadRequest("ID required.");
                await _firestoreService.UpdateQualificationAsync(userUid, qualification, documentFile);
                return Ok(new { success = true, message = "Qualification updated (status reset)." });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewMyDocument(string id)
        {
            try
            {
                string userUid = GetCurrentUserUid();
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("Missing required ID.");
                }

                var qualification = await _firestoreService.GetQualificationAsync(userUid, id);
                if (qualification == null || string.IsNullOrWhiteSpace(qualification.DocumentUrl))
                {
                    return NotFound("The qualification or its document could not be found.");
                }

                var (fileBytes, contentType) = await _firestoreService.DownloadFileAsync(qualification.DocumentUrl);
                Response.Headers.Append("Content-Disposition", new System.Net.Mime.ContentDisposition
                {
                    Inline = true,
                    FileName = $"qualification_{id}{Path.GetExtension(qualification.DocumentUrl)}"
                }.ToString());

                return new FileContentResult(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmployeeController] Error viewing document: {ex.Message}");
                return Content($"Error: {ex.Message}");
            }
        }
        [HttpGet]
        public async Task<IActionResult> ViewProfilePicture()
        {
            string defaultAvatar = "/images/default-avatar.png";
            try
            {
                string userUid = GetCurrentUserUid();
                var profile = await _firestoreService.GetEmployeeProfileAsync(userUid);

                if (profile == null || string.IsNullOrWhiteSpace(profile.ProfilePicture))
                {
                    return Redirect(defaultAvatar);
                }
                return Redirect(profile.ProfilePicture);
            }
            catch
            {
                return Redirect(defaultAvatar);
            }
        }

        [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> DeleteQualification(string id) { try { string userUid = GetCurrentUserUid(); if (string.IsNullOrEmpty(id)) return BadRequest("ID required."); await _firestoreService.DeleteQualificationAsync(userUid, id); return Ok(new { success = true, message = "Qualification deleted." }); } catch (UnauthorizedAccessException) { return Unauthorized(); } catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); } }
        [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> AddEmployeeTraining([FromBody] EmployeeTraining training) {try { string userUid = GetCurrentUserUid(); await _firestoreService.AddEmployeeTrainingAsync(userUid, training); return Ok(new { success = true, message = "Training submitted." }); } catch (UnauthorizedAccessException) { return Unauthorized(); } catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); } }
        [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> UpdateEmployeeTraining([FromBody] EmployeeTraining training) {  try { string userUid = GetCurrentUserUid(); if (string.IsNullOrEmpty(training.Id)) return BadRequest("ID required."); await _firestoreService.UpdateEmployeeTrainingAsync(userUid, training); return Ok(new { success = true, message = "Training updated (resubmitted)." }); } catch (UnauthorizedAccessException) { return Unauthorized(); } catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); } }
        [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> DeleteEmployeeTraining(string id) {try { string userUid = GetCurrentUserUid(); if (string.IsNullOrEmpty(id)) return BadRequest("ID required."); await _firestoreService.DeleteEmployeeTrainingAsync(userUid, id); return Ok(new { success = true, message = "Training deleted." }); } catch (UnauthorizedAccessException) { return Unauthorized(); } catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); } }
        public IActionResult ChangePassword() => View();
        //[HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model) { string? userEmail = User.FindFirstValue(ClaimTypes.Email); if (userEmail == null) return Unauthorized(); try { await _authService.SendPasswordResetEmailAsync(userEmail); TempData["SuccessMessage"] = "Password reset email sent."; return RedirectToAction("Profile"); } catch (Exception ex) { TempData["ErrorMessage"] = $"Error sending reset: {ex.Message}"; return RedirectToAction("ChangePassword"); } }
        [AllowAnonymous][ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)] public IActionResult Error() { return View(); }
        #endregion Other IActionResults
    }
}