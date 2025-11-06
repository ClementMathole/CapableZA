using Capableza.Web.Models;
using Capableza.Web.Services;
using Capableza.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Capableza.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly FirestoreService _firestoreService;
        private readonly AuthService _authService;

        public AdminController(FirestoreService firestoreService, AuthService authService)
        {
            _firestoreService = firestoreService;
            _authService = authService;
        }

        private string GetCurrentAdminUid() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("Admin user not authenticated.");

        public async Task<IActionResult> Index()
        {
            try
            {
                var employees = await _firestoreService.GetAllEmployeeProfilesAsync();
                var assignedTrainings = await _firestoreService.GetAllAssignedTrainingsAsync();
                var reports = await _firestoreService.GetAllReportsAsync();
                var recentLogs = await _firestoreService.GetRecentAuditLogsAsync(5);
                var allQualifications = new List<Qualification>();
                foreach (var emp in employees)
                {
                    var quals = await _firestoreService.GetEmployeeQualificationsAsync(emp.Uid);
                    foreach (var q in quals)
                    {
                        q.EmployeeName = emp.FullName;
                        q.EmployeeId = emp.Uid;
                        allQualifications.Add(q);
                    }
                }
                var pendingVerifications = allQualifications.Where(q => q.Status == "Pending").Take(3).ToList();
                int pendingQualCount = allQualifications.Count(q => q.Status == "Pending");
                var allSkills = new List<Skill>();
                foreach (var emp in employees)
                {
                    allSkills.AddRange(await _firestoreService.GetEmployeeSkillsAsync(emp.Uid));
                }
                var skillCategories = allSkills
                   .GroupBy(s => string.IsNullOrWhiteSpace(s.Category) ? "Other" : s.Category)
                   .Select(g => new { Category = g.Key, Count = g.Count() })
                   .OrderByDescending(x => x.Count)
                   .ToList();
                var topCategories = skillCategories.Take(3);
                var skillDistLabels = topCategories.Select(c => c.Category).ToList();
                var skillDistData = topCategories.Select(c => c.Count).ToList();
                int otherCount = skillCategories.Skip(3).Sum(c => c.Count);
                if (otherCount > 0)
                {
                    skillDistLabels.Add("Other");
                    skillDistData.Add(otherCount);
                }
                var skillAddedLogs = (await _firestoreService.GetRecentAuditLogsAsync(100))
                                       .Where(log => log.Action.Equals("skillAdded", StringComparison.OrdinalIgnoreCase))
                                       .OrderBy(log => log.TimestampDateTime)
                                       .ToList();
                var monthlyGrowth = skillAddedLogs
                    .GroupBy(log => new DateTime(log.TimestampDateTime.Year, log.TimestampDateTime.Month, 1))
                    .Select(g => new { Month = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Month)
                    .ToList();
                var cumulativeGrowthData = new List<int>();
                int cumulativeCount = 0;
                var skillGrowthLabels = new List<string>();
                int monthsToShow = 4;
                var recentMonthlyGrowth = monthlyGrowth.TakeLast(monthsToShow).ToList();
                foreach (var monthData in recentMonthlyGrowth)
                {
                    cumulativeCount += monthData.Count;
                    skillGrowthLabels.Add(monthData.Month.ToString("MMMM"));
                    cumulativeGrowthData.Add(cumulativeCount);
                }
                if (!skillGrowthLabels.Any())
                {
                    var now = DateTime.UtcNow;
                    skillGrowthLabels = Enumerable.Range(0, monthsToShow).Select(i => now.AddMonths(-(monthsToShow - 1 - i)).ToString("MMMM")).ToList();
                    cumulativeGrowthData = Enumerable.Repeat(0, monthsToShow).ToList();
                }
                var skillAddedLogsDaily = (await _firestoreService.GetRecentAuditLogsAsync(365))
                    .Where(log => log.Action.Equals("skillAdded", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(log => log.TimestampDateTime)
                    .ToList();
                var dailySkillCount = skillAddedLogsDaily
                    .GroupBy(log => log.TimestampDateTime.Date)
                    .ToDictionary(g => g.Key, g => g.Count());
                var sortedDates = dailySkillCount.Keys.OrderBy(d => d).ToList();
                var skillGrowthLabelsDaily = new List<string>();
                var cumulativeGrowthDataDaily = new List<int>();
                int cumulativeCountDaily = 0;
                foreach (var date in sortedDates)
                {
                    cumulativeCountDaily += dailySkillCount[date];
                    skillGrowthLabelsDaily.Add(date.ToString("MMM dd"));
                    cumulativeGrowthDataDaily.Add(cumulativeCountDaily);
                }

                if (!skillGrowthLabelsDaily.Any())
                {
                    var today = DateTime.UtcNow.Date;
                    skillGrowthLabelsDaily = Enumerable.Range(0, 7)
                        .Select(i => today.AddDays(-6 + i).ToString("MMM dd"))
                        .ToList();
                    cumulativeGrowthDataDaily = Enumerable.Repeat(0, 7).ToList();
                }


                ViewBag.EmployeeCount = employees.Count;
                ViewBag.TrainingCount = assignedTrainings.Count;
                ViewBag.ReportCount = reports.Count;
                ViewBag.PendingApprovalCount = pendingQualCount;
                ViewBag.PendingVerifications = pendingVerifications;
                ViewBag.RecentLogs = recentLogs;
                ViewBag.SkillDistLabelsJson = JsonConvert.SerializeObject(skillDistLabels);
                ViewBag.SkillDistDataJson = JsonConvert.SerializeObject(skillDistData);
                ViewBag.SkillGrowthLabelsJson = JsonConvert.SerializeObject(skillGrowthLabelsDaily);
                ViewBag.SkillGrowthDataJson = JsonConvert.SerializeObject(cumulativeGrowthDataDaily);
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Admin Dashboard: {ex}");
                TempData["ErrorMessage"] = "Error loading dashboard data. Please try again later.";
                return View();
            }
        }

        public async Task<IActionResult> Employees()
        {
            try {
                var employees = await _firestoreService.GetAllEmployeeProfilesAsync();
                return View(employees);
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error loading employees: {ex.Message}";
                return View(new List<EmployeeProfile>());
            }
        }


        public IActionResult AddEmployee() => View(new AddEmployeeViewModel());


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployee(AddEmployeeViewModel model)
        {
            if (ModelState.IsValid)
            {
                string? newEmployeeUid = null;
                try
                {
                    newEmployeeUid = await _authService.RegisterAuthUserAsync(model.Email, model.Password);

                    if (string.IsNullOrEmpty(newEmployeeUid))
                    {
                        throw new Exception("Authentication user creation failed unexpectedly.");
                    }
                    await _firestoreService.AddUserRoleAsync(newEmployeeUid, model.Email, "employee");
                    await _firestoreService.AddInitialEmployeeProfileAsync(newEmployeeUid, model.Email, model.FirstName, model.LastName);

                    TempData["SuccessMessage"] = $"Employee '{model.FirstName} {model.LastName}' added successfully with UID: {newEmployeeUid}.";
                    return RedirectToAction("Employees");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding employee: {ex}");
                }
            }
            return View(model);
        }

        public async Task<IActionResult> EmployeeDetails(string id) {
            if (string.IsNullOrEmpty(id))
                return BadRequest();
            try {
                var p = await _firestoreService.GetEmployeeProfileAsync(id);
                if (p == null)
                    return NotFound();
                ViewBag.Skills = await _firestoreService.GetEmployeeSkillsAsync(id);
                ViewBag.Qualifications = await _firestoreService.GetEmployeeQualificationsAsync(id);
                ViewBag.Trainings = await _firestoreService.GetEmployeeTrainingsAsync(id);
                return View(p);
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Employees");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployee(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid employee ID.";
                return RedirectToAction("Employees");
            }

            string adminUid = GetCurrentAdminUid();

            try
            {
                await _firestoreService.DeleteEmployeeAsync(id, adminUid);
                TempData["SuccessMessage"] = "Employee deleted successfully.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting employee: {ex.Message}");
                TempData["ErrorMessage"] = $"Error deleting employee: {ex.Message}";
            }

            return RedirectToAction("Employees");
        }

        public async Task<IActionResult> Trainings() {
            try {
                var t = await _firestoreService.GetAllAssignedTrainingsAsync();
                var e = await _firestoreService.GetAllEmployeeProfilesAsync();
                ViewBag.EmployeeMap = e.Where(x => !string.IsNullOrEmpty(x.Uid)).ToDictionary(x => x.Uid, x => x.FullName);
                return View(t);
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View(new List<AssignedTraining>());
            }
        }

        public async Task<IActionResult> AddTraining() {
            try {
                ViewBag.Employees = await _firestoreService.GetAllEmployeeProfilesAsync();
                return View(new AssignedTraining());
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Trainings");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTraining(AssignedTraining model) {
            if (Request.Form.ContainsKey("AssignedTo")) {
                model.AssignedTo = Request.Form["AssignedTo"].ToList();
            }
            if (model.AssignedTo == null || !model.AssignedTo.Any()) {
                ModelState.AddModelError("AssignedTo", "Select employee(s).");
            }
            if (
                !string.IsNullOrEmpty(model.StartDate) &&
                !string.IsNullOrEmpty(model.EndDate) &&
                DateTime.TryParse(model.EndDate, out var ed) &&
                DateTime.TryParse(model.StartDate, out var sd) && ed < sd) {
                ModelState.AddModelError("EndDate", "End date must be >= start date.");
            }
            if (ModelState.IsValid) {
                string adminUid = GetCurrentAdminUid();
                if (model.AssignedTo == null || !model.AssignedTo.Any()) {
                    ModelState.AddModelError("AssignedTo", "Select employee(s).");
                    ViewBag.Employees = await _firestoreService.GetAllEmployeeProfilesAsync();
                    return View(model);
                }
                try {
                    await _firestoreService.AssignTrainingAsync(model, adminUid);
                    TempData["SuccessMessage"] = "Training assigned.";
                    return RedirectToAction("Trainings");
                } catch (Exception ex) {
                    ModelState.AddModelError("", $"Error: {ex.Message}");
                }
            }
            ViewBag.Employees = await _firestoreService.GetAllEmployeeProfilesAsync();
            return View(model);
        }

        public async Task<IActionResult> EditTraining(string id)
        {
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("Trainings");

            try
            {
                var training = await _firestoreService.GetAssignedTrainingByIdAsync(id);
                if (training == null)
                {
                    TempData["ErrorMessage"] = "Training not found.";
                    return RedirectToAction("Trainings");
                }

                ViewBag.Employees = await _firestoreService.GetAllEmployeeProfilesAsync();
                return View("EditTraining", training);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading training: {ex.Message}";
                return RedirectToAction("Trainings");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTraining(AssignedTraining model)
        {
            if (Request.Form.ContainsKey("AssignedTo"))
            {
                model.AssignedTo = Request.Form["AssignedTo"].ToList();
            }

            if (model.AssignedTo == null || !model.AssignedTo.Any())
            {
                ModelState.AddModelError("AssignedTo", "Select employee(s).");
            }

            if (!string.IsNullOrEmpty(model.StartDate) &&
                !string.IsNullOrEmpty(model.EndDate) &&
                DateTime.TryParse(model.EndDate, out var ed) &&
                DateTime.TryParse(model.StartDate, out var sd) && ed < sd)
            {
                ModelState.AddModelError("EndDate", "End date must be >= start date.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Employees = await _firestoreService.GetAllEmployeeProfilesAsync();
                return View("EditTraining", model);
            }

            try
            {
                string adminUid = GetCurrentAdminUid();
                await _firestoreService.UpdateAssignedTrainingAsync(model, adminUid);

                TempData["SuccessMessage"] = "Training updated successfully.";
                return RedirectToAction("Trainings");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
                ViewBag.Employees = await _firestoreService.GetAllEmployeeProfilesAsync();
                return View("EditTraining", model);
            }
        }

        public async Task<IActionResult> Qualifications(string filter = "Pending") {
            try {
                var emps = await _firestoreService.GetAllEmployeeProfilesAsync();
                var allQ = new List<Qualification>();
                foreach (var emp in emps) {
                    var qs = await _firestoreService.GetEmployeeQualificationsAsync(emp.Uid);
                    foreach (var q in qs) {
                        q.EmployeeName = emp.FullName;
                        q.EmployeeId = emp.Uid;
                        q.ProfilePicture = emp.ProfilePicture;
                    }
                    allQ.AddRange(qs);
                }

                var fQ = filter.ToLower() switch {
                    "pending" => allQ.Where(q => q.Status == "Pending").ToList(),
                    "verified" => allQ.Where(q => q.Status == "Verified").ToList(),
                    "rejected" => allQ.Where(q => q.Status == "Rejected").ToList(),
                    _ => allQ
                };
                ViewBag.CurrentFilter = filter; return View(fQ);
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View(new List<Qualification>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewQualificationDocument(string employeeId, string qualificationId)
        {
            try
            {
                if (string.IsNullOrEmpty(employeeId) || string.IsNullOrEmpty(qualificationId))
                {
                    return BadRequest("Missing required IDs.");
                }
                var qualification = await _firestoreService.GetQualificationAsync(employeeId, qualificationId);
                if (qualification == null || string.IsNullOrWhiteSpace(qualification.DocumentUrl))
                {
                    return NotFound("The qualification or its document could not be found.");
                }

                var (fileBytes, contentType) = await _firestoreService.DownloadFileAsync(qualification.DocumentUrl);
                Response.Headers.Append("Content-Disposition", new System.Net.Mime.ContentDisposition
                {
                    Inline = true,
                    FileName = $"qualification_{qualificationId}{Path.GetExtension(qualification.DocumentUrl)}"
                }.ToString());

                return new FileContentResult(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminController] Error viewing document: {ex.Message}");
                return Content($"Error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyQualification(string employeeId, string qualificationId) {
            if (string.IsNullOrEmpty(employeeId) || string.IsNullOrEmpty(qualificationId))
                return BadRequest();
            string cf = Request.Form["currentFilter"].FirstOrDefault() ?? "Pending";
            try {
                await _firestoreService.VerifyQualificationAsync(employeeId, qualificationId);
                TempData["SuccessMessage"] = "Verified.";
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }
            return RedirectToAction("Qualifications", new { filter = cf });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectQualification(string employeeId, string qualificationId) {
            if (string.IsNullOrEmpty(employeeId) || string.IsNullOrEmpty(qualificationId))
                return BadRequest();

            string cf = Request.Form["currentFilter"].FirstOrDefault() ?? "Pending";
            try {
                await _firestoreService.RejectQualificationAsync(employeeId, qualificationId);
                TempData["SuccessMessage"] = "Rejected.";
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }
            return RedirectToAction("Qualifications", new { filter = cf });
        }

        public async Task<IActionResult> Skills() {
            try {
                var emps = await _firestoreService.GetAllEmployeeProfilesAsync();
                var allS = new List<Skill>();
                foreach (var emp in emps) {
                    var ss = await _firestoreService.GetEmployeeSkillsAsync(emp.Uid);
                    foreach (var s in ss) {
                        s.EmployeeName = emp.FullName; s.EmployeeId = emp.Uid;
                    }
                    allS.AddRange(ss);
                }
                allS = allS.OrderBy(s => s.EmployeeName).ThenBy(s => s.SkillName).ToList();
                return View(allS);
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View(new List<Skill>());
            }
        }

        public async Task<IActionResult> Reports()
        {
            try
            {
                var reports = await _firestoreService.GetAllReportsAsync();
                return View(reports);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading reports: {ex.Message}";
                return View(new List<Report>());
            }
        }
        

        public async Task<IActionResult> DeleteReport(string reportId)
        {
            string adminUid = GetCurrentAdminUid();
            if (string.IsNullOrEmpty(reportId)) return BadRequest();
            try
            {
                await _firestoreService.DeleteReportAsync(reportId, adminUid);
                TempData["SuccessMessage"] = "Report deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting report: {ex.Message}";
            }
            return RedirectToAction("Reports");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReport(string reportType, string positionOrRole, string dateRange)
        {
            if (string.IsNullOrEmpty(reportType) || reportType == "-- Select Type --")
            {
                TempData["ErrorMessage"] = "Please select a valid report type.";
                return RedirectToAction("Reports");
            }
            string adminUid = GetCurrentAdminUid();
            string generatedFileName = $"Report_{reportType.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMddHHmm}.pdf";

            try
            {
                var allEmployees = await _firestoreService.GetAllEmployeeProfilesAsync();
                byte[] pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.Header().Text($"National Treasury Skills Portal - Report").SemiBold().FontSize(16).FontColor("#0F6F84");
                        page.Content().Column(col =>
                        {
                            col.Spacing(10);
                            col.Item().Text($"Report Type: {reportType}").Bold();
                            col.Item().Text($"Parameters: {positionOrRole} | {dateRange}");
                            col.Item().Text($"Generated On: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                            col.Item().PaddingTop(15).Text("Report Data").SemiBold().FontSize(14);
                            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Employee Name").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Job Title").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Email").SemiBold();
                                });

                                foreach (var emp in allEmployees.Take(20))
                                {
                                    table.Cell().Element(CellStyle).Text(emp.FullName ?? "-");
                                    table.Cell().Element(CellStyle).Text(emp.JobTitle ?? "-");
                                    table.Cell().Element(CellStyle).Text(emp.Email ?? "-");
                                }
                                static IContainer CellStyle(IContainer container) =>
                                    container.PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                            });
                        });
                        page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
                    });
                }).GeneratePdf();

                string fileUrl = await _firestoreService.UploadReportToStorageAsync(pdfBytes, generatedFileName, adminUid);
                var report = new Report
                {
                    ReportType = reportType,
                    PositionOrRole = positionOrRole,
                    DateRange = dateRange,
                    GeneratedBy = adminUid,
                    FileUrl = fileUrl,
                };
                await _firestoreService.AddReportAsync(report);

                TempData["SuccessMessage"] = $"Report '{reportType}' generated successfully and saved.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL: Error generating report: {ex}");
                TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
            }
            return RedirectToAction("Reports");
        }

        [HttpGet] 
        public async Task<IActionResult> ReportDetails(string id)
        {
            ViewData["Title"] = "Report Details";
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Report ID is required.");
            }
            try
            {
                var report = await _firestoreService.GetReportAsync(id);
                if (report == null)
                {
                    return NotFound("Report not found.");
                }
                return View(report);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading report details: {ex.Message}";
                return RedirectToAction("Reports");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewReport(string id)
        {
            try
            {
                var report = await _firestoreService.GetReportAsync(id);
                if (report == null || string.IsNullOrEmpty(report.FileUrl))
                {
                    return NotFound("Report file not found.");
                }
                using (var httpClient = new HttpClient())
                {
                    var pdfBytes = await httpClient.GetByteArrayAsync(report.FileUrl);
                    return new FileContentResult(pdfBytes, "application/pdf");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error viewing report {id}: {ex.Message}");
                return Content($"Error loading PDF: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerateCv(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("Employee ID is required.");

            try
            {
                var profileTask = _firestoreService.GetEmployeeProfileAsync(id);
                var skillsTask = _firestoreService.GetEmployeeSkillsAsync(id);
                var qualificationsTask = _firestoreService.GetEmployeeQualificationsAsync(id);
                var trainingsTask = _firestoreService.GetEmployeeTrainingsAsync(id);

                await Task.WhenAll(profileTask, skillsTask, qualificationsTask, trainingsTask);

                var profile = profileTask.Result;
                var skills = skillsTask.Result;
                var qualifications = qualificationsTask.Result;
                var trainings = trainingsTask.Result;

                if (profile == null)
                    return NotFound("Employee profile not found.");

                var pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.DefaultTextStyle(style => style.FontSize(11).FontFamily(Fonts.Arial));
                        page.Header().Column(col =>
                        {
                            col.Item()
                               .PaddingTop(10)
                               .Text(profile.FullName)
                               .Bold()
                               .FontSize(24)
                               .FontColor("#0F6F84");

                            col.Item()
                               .Text(profile.JobTitle)
                               .SemiBold()
                               .FontSize(16)
                               .FontColor("#495057");

                            col.Item()
                               .LineHorizontal(1);
                        });

                        page.Content().Column(col =>
                        {
                            col.Spacing(20);
                            col.Item().Row(row =>
                            {
                                row.Spacing(20);
                                row.RelativeColumn().Column(c =>
                                {
                                    c.Item().Text("Email Address")
                                     .SemiBold()
                                     .FontSize(10)
                                     .FontColor(Colors.Grey.Medium);
                                    c.Item().Text(profile.Email);
                                });

                                row.RelativeColumn().Column(c =>
                                {
                                    c.Item().Text("SA ID Number")
                                     .SemiBold()
                                     .FontSize(10)
                                     .FontColor(Colors.Grey.Medium);
                                    c.Item().Text(profile.IdNumber ?? "N/A");
                                });
                            });

                            col.Item().Column(c =>
                            {
                                c.Item()
                                 .Text("Professional Skills")
                                 .Bold()
                                 .FontSize(14)
                                 .FontColor("#0F6F84");

                                if (skills.Any())
                                {
                                    c.Item().PaddingTop(10).Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(1);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Text("Skill").SemiBold();
                                            header.Cell().Text("Category").SemiBold();
                                            header.Cell().Text("Proficiency").SemiBold().AlignRight();
                                        });

                                        foreach (var skill in skills.OrderByDescending(s => s.Proficiency))
                                        {
                                            table.Cell().Text(skill.SkillName);
                                            table.Cell().Text(skill.Category).FontColor(Colors.Grey.Medium);
                                            table.Cell().AlignRight().Text($"{skill.Proficiency}%");
                                        }
                                    });
                                }
                                else
                                {
                                    c.Item()
                                     .PaddingTop(5)
                                     .Text("No skills recorded.")
                                     .Italic()
                                     .FontColor(Colors.Grey.Medium);
                                }
                            });

                            col.Item().Column(c =>
                            {
                                c.Item()
                                 .Text("Qualifications")
                                 .Bold()
                                 .FontSize(14)
                                 .FontColor("#0F6F84");

                                if (qualifications.Any())
                                {
                                    foreach (var qual in qualifications.OrderByDescending(q => q.YearObtained))
                                    {
                                        c.Item().PaddingTop(10).Text(text =>
                                        {
                                            text.Span($"{qual.Title} ").SemiBold();
                                            text.Span($"({qual.YearObtained})").FontColor(Colors.Grey.Medium);
                                        });

                                        c.Item().Text(qual.Institute).Italic();
                                        c.Item().Text(qual.Type)
                                         .FontSize(9)
                                         .FontColor(Colors.Grey.Darken1);
                                    }
                                }
                                else
                                {
                                    c.Item()
                                     .PaddingTop(5)
                                     .Text("No qualifications.")
                                     .Italic()
                                     .FontColor(Colors.Grey.Medium);
                                }
                            });
                            col.Item().Column(c =>
                            {
                                c.Item()
                                 .Text("Completed Training")
                                 .Bold()
                                 .FontSize(14)
                                 .FontColor("#0F6F84");

                                var completedTrainings = trainings
                                    .Where(t => t.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) && t.Approved)
                                    .ToList();

                                if (completedTrainings.Any())
                                {
                                    foreach (var train in completedTrainings.OrderByDescending(t => t.EndDate))
                                    {
                                        c.Item().PaddingTop(10).Text(text =>
                                        {
                                            text.Span($"{train.TrainingName} ").SemiBold();
                                            text.Span($"({train.StartDate:dd MMM yyyy} - {train.EndDate:dd MMM yyyy})")
                                                .FontColor(Colors.Grey.Medium);
                                        });
                                        c.Item().Text(train.Provider).Italic();
                                    }
                                }
                                else
                                {
                                    c.Item()
                                     .PaddingTop(5)
                                     .Text("No approved, completed trainings recorded.")
                                     .Italic()
                                     .FontColor(Colors.Grey.Medium);
                                }
                            });

                        });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                            });
                    });
                })
                .GeneratePdf();
                string safeName = string.Join("_", profile.FullName.Split(Path.GetInvalidFileNameChars()));
                string cvFileName = $"{safeName}_CV_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", cvFileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating CV for {id}: {ex.Message}\n{ex.StackTrace}");
                TempData["ErrorMessage"] = $"Could not generate CV: {ex.Message}";
                return RedirectToAction("EmployeeDetails", new { id });
            }
        }



        public async Task<IActionResult> Notifications(string filter = "All", string search = "")
        {
            ViewData["Title"] = "Notifications";
            try
            {
                var allNotifications = await _firestoreService.GetAllNotificationsAsync();
                int countAll = allNotifications.Count;
                int countUnread = allNotifications.Count(n => n.IsUnread);
                int countApprovals = allNotifications.Count(n => n.Title.Contains("Approval", StringComparison.OrdinalIgnoreCase));
                int countMessages = allNotifications.Count(n => n.Type.Equals("message", StringComparison.OrdinalIgnoreCase));
                int countSystemAlerts = allNotifications.Count(n => n.Type.Equals("alert", StringComparison.OrdinalIgnoreCase));
                var filteredNotifications = allNotifications;


                filter = filter?.ToLower() ?? "all";
                filteredNotifications = filter switch
                {
                    "unread" => filteredNotifications.Where(n => n.IsUnread).ToList(),
                    "approvals" => filteredNotifications.Where(n => n.Title.Contains("Approval", StringComparison.OrdinalIgnoreCase)).ToList(),
                    "messages" => filteredNotifications.Where(n => n.Type.Equals("message", StringComparison.OrdinalIgnoreCase)).ToList(),
                    "system alerts" => filteredNotifications.Where(n => n.Type.Equals("alert", StringComparison.OrdinalIgnoreCase)).ToList(),
                    _ => filteredNotifications
                };

                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    filteredNotifications = filteredNotifications
                        .Where(n => n.Title.ToLower().Contains(search) || n.Description.ToLower().Contains(search))
                        .ToList();
                }

                ViewBag.CountAll = countAll;
                ViewBag.CountUnread = countUnread;
                ViewBag.CountApprovals = countApprovals;
                ViewBag.CountMessages = countMessages;
                ViewBag.CountSystemAlerts = countSystemAlerts;
                ViewBag.CurrentFilter = filter;
                ViewBag.CurrentSearch = search;

                return View(filteredNotifications);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading notifications: {ex.Message}";
                return View(new List<AppNotification>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            try { await _firestoreService.MarkAllNotificationsReadAsync(); TempData["SuccessMessage"] = "All notifications marked as read."; }
            catch (Exception ex) { TempData["ErrorMessage"] = $"Error: {ex.Message}"; }
            return RedirectToAction("Notifications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationRead(string notificationId, string currentFilter = "All")
        {
            if (string.IsNullOrEmpty(notificationId)) return BadRequest("Notification ID required.");
            try
            {
                await _firestoreService.MarkNotificationReadAsync(notificationId);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true });
                }
                TempData["SuccessMessage"] = "Notification marked as read.";
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = ex.Message });
                }
                TempData["ErrorMessage"] = $"Error marking read: {ex.Message}";
            }
            return RedirectToAction("Notifications", new { filter = currentFilter });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeAdminPassword(ChangeAdminPasswordViewModel model)
        {
            string? adminEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(adminEmail))
            {
                TempData["ErrorMessage"] = "Could not find your email. Please log in again.";
                return RedirectToAction("Settings");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Password change failed. Please check the errors below.";
                return View("Settings", model);
            }

            try
            {
                var authResponse = await _authService.LoginAsync(adminEmail, model.CurrentPassword);

                if (authResponse == null || string.IsNullOrEmpty(authResponse.IdToken))
                {
                    throw new Exception("Re-authentication failed (no token received).");
                }

                await _authService.ChangePasswordAsync(authResponse.IdToken, model.NewPassword);
                TempData["SuccessMessage"] = "Password updated successfully. Please log in again with your new password.";
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View("Settings", model);
            }
        }
        public IActionResult Settings()
        {
            var model = new ChangeAdminPasswordViewModel();
            return View(model);
        }

        public async Task<IActionResult> AuditLog() {
            try {
                var logs = await _firestoreService.GetRecentAuditLogsAsync(50);
                return View(logs);
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View(new List<AuditLog>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTraining(string employeeId, string trainingId) {
            if (string.IsNullOrEmpty(employeeId) || string.IsNullOrEmpty(trainingId))
                return BadRequest();

            try {
                await _firestoreService.ApproveEmployeeTrainingAsync(employeeId, trainingId); TempData["SuccessMessage"] = "Approved.";
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }
            return RedirectToAction("EmployeeDetails", new { id = employeeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTraining(string employeeId, string trainingId) {
            if (string.IsNullOrEmpty(employeeId) || string.IsNullOrEmpty(trainingId))
                return BadRequest();

            try {
                await _firestoreService.RejectEmployeeTrainingAsync(employeeId, trainingId);
                TempData["SuccessMessage"] = "Rejected.";
            } catch (Exception ex) {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }
            return RedirectToAction("EmployeeDetails", new { id = employeeId });
        }

        [HttpGet]
        public async Task<IActionResult> SkillDetails(string employeeId, string id)
        {
            if (string.IsNullOrEmpty(employeeId) || string.IsNullOrEmpty(id)) return BadRequest();

            var skill = await _firestoreService.GetSkillAsync(employeeId, id);
            if (skill == null) return NotFound("Skill not found.");

            var employee = await _firestoreService.GetEmployeeProfileAsync(employeeId);
            ViewBag.EmployeeName = employee?.FullName ?? "Unknown Employee";
            ViewBag.EmployeeId = employeeId;

            return View(skill);
        }

        [HttpGet]
        public async Task<IActionResult> QualificationDetails(string employeeId, string id)
        {
            if (string.IsNullOrEmpty(employeeId) || string.IsNullOrEmpty(id)) return BadRequest();

            var qualification = await _firestoreService.GetQualificationAsync(employeeId, id);
            if (qualification == null) return NotFound("Qualification not found.");

            var employee = await _firestoreService.GetEmployeeProfileAsync(employeeId);
            ViewBag.EmployeeName = employee?.FullName ?? "Unknown Employee";
            ViewBag.EmployeeId = employeeId;

            return View(qualification);
        }

        [HttpGet]
        public async Task<IActionResult> TrainingDetails(string employeeId, string id)
        {
            if (string.IsNullOrEmpty(employeeId) || string.IsNullOrEmpty(id)) return BadRequest();

            var training = await _firestoreService.GetEmployeeTrainingAsync(employeeId, id);
            if (training == null) return NotFound("Training record not found.");

            var employee = await _firestoreService.GetEmployeeProfileAsync(employeeId);
            ViewBag.EmployeeName = employee?.FullName ?? "Unknown Employee";
            ViewBag.EmployeeId = employeeId;

            return View(training);
        }

        [HttpGet]
        public async Task<IActionResult> ViewEmployeeProfilePicture(string id)
        {
            string defaultAvatar = "/images/default-avatar.png";
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Redirect(defaultAvatar);
                }

                var profile = await _firestoreService.GetEmployeeProfileAsync(id);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAssignedTraining(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid training ID.";
                return RedirectToAction("Trainings");
            }

            string adminUid;
            try
            {
                adminUid = GetCurrentAdminUid();
            }
            catch (UnauthorizedAccessException)
            {
                return Challenge();
            }

            try
            {
                await _firestoreService.DeleteAssignedTrainingAsync(id, adminUid);
                TempData["SuccessMessage"] = "Training assignment deleted successfully.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting assigned training: {ex.Message}");
                TempData["ErrorMessage"] = $"Error deleting training: {ex.Message}";
            }
            return RedirectToAction("Trainings");
        }
    }
}