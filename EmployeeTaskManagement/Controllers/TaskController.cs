using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity;

namespace EmployeeTaskManagement.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        [HttpPost, Authorize(Roles = "Admin,Manager"), ValidateAntiForgeryToken]
        public ActionResult CreateTask(
            string title,
            string description,
            int assignedEmployeeId,
            string priority,
            DateTime? startDate,
            DateTime? dueDate,
            int? projectId)
        {
            var userId = User.Identity.GetUserId();
            var creator = db.Employees.FirstOrDefault(employee => employee.UserId == userId);
            var isAdmin = RoleAccess.IsAdministrator(User);
            var assignee = db.Employees.Find(assignedEmployeeId);
            var project = projectId.HasValue
                ? db.Projects.Include(item => item.Members).FirstOrDefault(item => item.ProjectId == projectId.Value)
                : null;
            var assigneeHasUserRole = !isAdmin && EmployeeEligibility.ProjectMembers(db)
                .Any(employee => employee.EmployeeId == assignedEmployeeId);

            if ((projectId.HasValue && project == null)
                || !RoleAccess.CanAssignTask(User, creator, project, assignee, assigneeHasUserRole))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            if (!ModelState.IsValid || String.IsNullOrWhiteSpace(title) || title.Trim().Length > 200
                || (description ?? "").Length > 2000 || (creator != null && !creator.IsActive)
                || !(new[] { "Low", "Medium", "High" }).Contains(priority)
                || (startDate.HasValue && dueDate.HasValue && dueDate.Value.Date < startDate.Value.Date)
                || (project != null && project.DueDate.HasValue && dueDate.HasValue
                    && dueDate.Value.Date > project.DueDate.Value.Date))
            {
                TempData["WorkspaceError"] = "Check the task details, dates, and active employee profile.";
                return RedirectToAction(isAdmin ? "AdminTasks" : "Tasks", "Home");
            }

            // Older administrator accounts may not have an Employee record. Link the
            // authenticated account in the same SaveChanges operation as the task.
            if (creator == null)
            {
                var account = db.Users.Find(userId);
                if (!isAdmin || account == null || String.IsNullOrWhiteSpace(account.Email) || account.Email.Length > 150)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.Conflict,
                        "A valid employee profile or account email is required to create a task.");
                }

                var fullName = account.UserName ?? account.Email;
                creator = new Employee
                {
                    UserId = account.Id,
                    FullName = fullName.Length > 100 ? fullName.Substring(0, 100) : fullName,
                    Email = account.Email,
                    Position = "Admin",
                    HireDate = DateTime.Today,
                    CreatedByUserId = account.Id,
                    IsActive = true
                };
                db.Employees.Add(creator);
            }

            var task = new EmployeeTaskManagement.Models.Task
            {
                Title = title.Trim(),
                Description = description,
                AssignedEmployeeId = assignedEmployeeId,
                CreatedByEmployee = creator,
                ProjectId = projectId,
                Priority = priority,
                StartDate = startDate,
                DueDate = dueDate,
                Status = "Pending"
            };
            task.Histories.Add(new TaskHistory { Employee = creator, Action = "Created", Details = "Task created." });
            db.Tasks.Add(task);
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Task created.";
            return RedirectToAction(isAdmin ? "AdminTasks" : "Tasks", "Home");
        }

        [HttpGet]
        public ActionResult TaskDetails(int id)
        {
            var task = db.Tasks.Include(t => t.AssignedEmployee).Include(t => t.AssignedEmployee.Department)
                .Include(t => t.CreatedByEmployee)
                .Include(t => t.Project)
                .Include(t => t.Project.ProjectManager)
                .Include(t => t.Project.Members.Select(member => member.Employee))
                .Include(t => t.Comments.Select(c => c.Employee))
                .Include(t => t.Histories.Select(h => h.Employee))
                .Include(t => t.ProgressLogs.Select(log => log.User))
                .Include(t => t.Attachments)
                .FirstOrDefault(t => t.TaskId == id);
            if (task == null)
            {
                return HttpNotFound();
            }

            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(e => e.UserId == userId);
            if (!RoleAccess.CanViewTask(User, employee, task))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            ViewBag.IsAssignee = employee != null && employee.IsActive && task.AssignedEmployeeId == employee.EmployeeId;
            ViewBag.CanCommentTask = RoleAccess.IsAdministrator(User) || ViewBag.IsAssignee == true;
            ViewBag.CanReviewJob = RoleAccess.IsAdministrator(User);
            ViewBag.CanManageTask = RoleAccess.IsAdministrator(User);
            ViewBag.CanDeleteData = User.IsInRole("Admin");
            ViewBag.DeadlineRequests = db.TaskDeadlineRequests.Include(request => request.RequestedBy).Include(request => request.DecidedBy)
                .Where(request => request.TaskId == id)
                .OrderByDescending(request => request.CreatedAt)
                .ToList();
            ViewBag.ProjectProgressReports = db.ProjectProgressReports.Include(report => report.Reporter).Include(report => report.Recipient)
                .Include(report => report.Attachments)
                .Where(report => report.TaskId == id)
                .OrderByDescending(report => report.CreatedAt)
                .ToList();
            return View(task);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AcceptJob(int id)
        {
            var task = db.Tasks.Find(id);
            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId);
            if (task == null)
            {
                return HttpNotFound();
            }

            if (employee == null || task.AssignedEmployeeId != employee.EmployeeId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            if (task.Status != "Pending")
            {
                TempData["WorkspaceError"] = "Only pending jobs can be accepted.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            if (task.ProjectId.HasValue
                && !db.Projects.Any(activeProject => activeProject.ProjectId == task.ProjectId.Value && activeProject.Status == "In Progress"))
            {
                TempData["WorkspaceError"] = "This project is not active, so its tasks cannot be accepted yet.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            task.Status = "Confirmed";
            task.UpdatedAt = DateTime.UtcNow;
            db.JobProgressLogs.Add(new JobProgressLog
            {
                TaskId = id,
                UserId = userId,
                Status = "confirmed",
                Percent = task.Progress,
                Note = "Job accepted."
            });
            task.Histories.Add(new TaskHistory
            {
                EmployeeId = employee.EmployeeId,
                Action = "Confirmed",
                Details = "Assigned user accepted the job."
            });
            db.SaveChanges();
            return RedirectToAction("TaskDetails", new
            {
                id
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SubmitJobProgress(
            int id,
            string progressStatus,
            int percent,
            string note,
            IEnumerable<HttpPostedFileBase> attachments,
            int? recipientEmployeeId)
        {
            var task = db.Tasks.Find(id);
            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId);
            if (task == null)
            {
                return HttpNotFound();
            }

            if (employee == null || task.AssignedEmployeeId != employee.EmployeeId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            if (task.Status != "Confirmed" && task.Status != "In Progress" && task.Status != "On Hold")
            {
                TempData["WorkspaceError"] = "Accept the job before submitting progress. Jobs pending review cannot be changed by the assignee.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            if (task.ProjectId.HasValue
                && !db.Projects.Any(activeProject => activeProject.ProjectId == task.ProjectId.Value && activeProject.Status == "In Progress"))
            {
                TempData["WorkspaceError"] = "This project is paused or not active. Progress cannot be updated.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            var validStatuses = new[] { "not_started", "in_progress", "on_hold", "completed" };
            if (!validStatuses.Contains(progressStatus) || percent < 0 || percent > 100 || (note ?? "").Length > 2000
                || (progressStatus == "not_started" && percent != 0)
                || (progressStatus == "in_progress" && (percent < 1 || percent > 99)))
            {
                TempData["WorkspaceError"] = "Choose a valid progress status and percentage (0–100).";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }

            var project = task.ProjectId.HasValue
                ? db.Projects.Include(item => item.Members).FirstOrDefault(item => item.ProjectId == task.ProjectId.Value)
                : null;
            var recipient = recipientEmployeeId.HasValue ? db.Employees.Find(recipientEmployeeId.Value) : null;
            if (project != null
                && (recipient == null
                || (project.ProjectManagerEmployeeId != recipient.EmployeeId
                && !project.Members.Any(member => member.EmployeeId == recipient.EmployeeId))))
            {
                TempData["WorkspaceError"] = "Choose a recipient who belongs to this project.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            var taskCompleted = percent == 100 || progressStatus == "completed";
            if (!taskCompleted && String.IsNullOrWhiteSpace(note))
            {
                TempData["WorkspaceError"] = "Add a comment explaining why this task is not complete.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }

            var files = (attachments ?? Enumerable.Empty<HttpPostedFileBase>()).Where(file => file != null && file.ContentLength > 0).ToList();
            var validatedFiles = new List<Tuple<HttpPostedFileBase, string, string>>();
            foreach (var file in files)
            {
                string expectedMime;
                var validationMessage = ValidateJobFile(file, out expectedMime);
                if (validationMessage != null)
                {
                    TempData["WorkspaceError"] = validationMessage;
                    return RedirectToAction("TaskDetails", new
                    {
                        id
                    });
                }
                validatedFiles.Add(Tuple.Create(file, expectedMime, Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant()));
            }

            if (percent == 100 || progressStatus == "completed")
            {
                progressStatus = "completed";
                percent = 100;
                task.Status = "Completed Pending Review";
                task.CompletedDate = null;
            }
            else if (progressStatus == "on_hold")
            {
                task.Status = "On Hold";
            }
            else if (progressStatus == "in_progress")
            {
                task.Status = "In Progress";
            }
            else
            {
                task.Status = "Confirmed";
            }

            task.Progress = percent;
            task.UpdatedAt = DateTime.UtcNow;
            var progressLog = new JobProgressLog
            {
                TaskId = id,
                UserId = userId,
                Status = progressStatus,
                Percent = percent,
                Note = String.IsNullOrWhiteSpace(note) ? null : note.Trim()
            };
            db.JobProgressLogs.Add(progressLog);
            ProjectProgressReport projectReport = null;
            if (project != null)
            {
                projectReport = new ProjectProgressReport
                {
                    ProjectId = project.ProjectId,
                    TaskId = id,
                    ReporterUserId = userId,
                    RecipientEmployeeId = recipient.EmployeeId,
                    TaskCompleted = taskCompleted,
                    ReportType = "TaskUpdate",
                    Note = String.IsNullOrWhiteSpace(note) ? null : note.Trim()
                };
                db.ProjectProgressReports.Add(projectReport);
            }
            task.Histories.Add(new TaskHistory
            {
                TaskId = id,
                EmployeeId = employee.EmployeeId,
                Action = "Progress Report",
                Details = progressStatus + " — " + percent + "%" + (String.IsNullOrWhiteSpace(note) ? "" : "; " + note.Trim())
            });

            var savedPaths = new List<string>();
            var storageDirectory = Server.MapPath("~/App_Data/JobAttachments");
            try
            {
                if (validatedFiles.Count > 0)
                {
                    Directory.CreateDirectory(storageDirectory);
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    foreach (var item in validatedFiles)
                    {
                        var postedFile = item.Item1;
                        var storedName = Guid.NewGuid().ToString("N") + item.Item3;
                        var destination = Path.Combine(storageDirectory, storedName);
                        postedFile.InputStream.Position = 0;
                        postedFile.SaveAs(destination);
                        savedPaths.Add(destination);
                        db.JobAttachments.Add(new JobAttachment
                        {
                            TaskId = id,
                            FileName = Path.GetFileName(postedFile.FileName),
                            StoredFileName = storedName,
                            MimeType = item.Item2,
                            SizeBytes = postedFile.ContentLength,
                            UploadedByUserId = userId,
                            UploadedAt = DateTime.UtcNow
                        });
                        if (projectReport != null)
                        {
                            projectReport.Attachments.Add(new ProjectReportAttachment
                            {
                                FileName = Path.GetFileName(postedFile.FileName),
                                StoredFileName = storedName,
                                MimeType = item.Item2,
                                SizeBytes = postedFile.ContentLength,
                                UploadedByUserId = userId,
                                UploadedAt = DateTime.UtcNow
                            });
                        }
                    }
                    db.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (Exception exception)
            {
                foreach (var savedPath in savedPaths)
                {
                    if (System.IO.File.Exists(savedPath))
                    {
                        System.IO.File.Delete(savedPath);
                    }
                }

                System.Diagnostics.Trace.TraceError("Job progress or attachment save failed: {0}", exception);
                TempData["WorkspaceError"] = "The progress report could not be saved. Try again or contact support.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            TempData["WorkspaceSuccess"] = "Progress report saved.";
            return RedirectToAction("TaskDetails", new
            {
                id
            });
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult ReviewJob(int id, bool approve, string note)
        {
            var task = db.Tasks.Find(id);
            if (task == null)
            {
                return HttpNotFound();
            }

            var userId = User.Identity.GetUserId();
            var currentEmployee = db.Employees.FirstOrDefault(employee => employee.UserId == userId);
            if (task.Status != "Completed Pending Review")
            {
                TempData["WorkspaceError"] = "Only completed jobs awaiting review can be reviewed.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            if ((note ?? "").Length > 1000)
            {
                TempData["WorkspaceError"] = "Review notes must be 1,000 characters or fewer.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            var reviewerEmployee = currentEmployee
                ?? db.Employees.Find(task.CreatedByEmployeeId);
            if (reviewerEmployee == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Conflict, "Link the administrator to an employee record before reviewing jobs.");
            }

            var previousStatus = task.Status;
            task.Status = approve ? "Completed" : "In Progress";
            task.Progress = approve ? 100 : Math.Min(task.Progress, 99);
            task.CompletedDate = approve ? (DateTime?)DateTime.UtcNow : null;
            task.UpdatedAt = DateTime.UtcNow;
            var details = (approve ? "Approved" : "Returned for rework") + (String.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim());
            if (reviewerEmployee.UserId != userId)
            {
                details = "Admin " + User.Identity.Name + " — " + details;
            }

            db.JobProgressLogs.Add(new JobProgressLog
            {
                TaskId = id,
                UserId = userId,
                Status = approve ? "approved" : "returned",
                Percent = task.Progress,
                Note = String.IsNullOrWhiteSpace(note) ? details : note.Trim()
            });
            task.Histories.Add(new TaskHistory
            {
                TaskId = id,
                EmployeeId = reviewerEmployee.EmployeeId,
                Action = "Review",
                Details = previousStatus + " → " + task.Status + "; " + details
            });
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = approve ? "Job approved and closed." : "Job returned to the assignee for further work.";
            return RedirectToAction("TaskDetails", new
            {
                id
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RequestTaskDeadline(int id, DateTime requestedDueDate, string reason)
        {
            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId && item.IsActive);
            var task = db.Tasks.Find(id);
            if (task == null)
            {
                return HttpNotFound();
            }

            if (employee == null || task.AssignedEmployeeId != employee.EmployeeId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var alreadyPending = db.TaskDeadlineRequests.Any(request => request.TaskId == id && request.Status == "Pending");
            var projectDeadline = task.ProjectId.HasValue
                ? db.Projects.Where(project => project.ProjectId == task.ProjectId.Value).Select(project => project.DueDate)
                .FirstOrDefault()
                : null;
            if (alreadyPending || String.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000
                || requestedDueDate.Date <= DateTime.Today
                || (task.DueDate.HasValue && requestedDueDate.Date <= task.DueDate.Value.Date)
                || (projectDeadline.HasValue && requestedDueDate.Date > projectDeadline.Value.Date)
                || task.Status == "Completed"
                || task.Status == "Completed Pending Review"
                || task.Status == "Cancelled")
            {
                TempData["WorkspaceError"] = alreadyPending
                    ? "A deadline request is already awaiting a decision."
                    : "Enter a later deadline and explain why an extension is needed.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            db.TaskDeadlineRequests.Add(new TaskDeadlineRequest
            {
                TaskId = id,
                RequestedByUserId = userId,
                RequestedDueDate = requestedDueDate.Date,
                Reason = reason.Trim()
            });
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Deadline extension request sent to the task owner.";
            return RedirectToAction("TaskDetails", new
            {
                id
            });
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DecideTaskDeadline(int id, int requestId, bool approve, string decisionNote)
        {
            var task = db.Tasks.Find(id);
            var request = db.TaskDeadlineRequests.FirstOrDefault(item => item.TaskDeadlineRequestId == requestId && item.TaskId == id);
            if (task == null || request == null)
            {
                return HttpNotFound();
            }

            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId);
            if (request.Status != "Pending" || (decisionNote ?? "").Length > 1000)
            {
                TempData["WorkspaceError"] = "This deadline request has already been decided or the decision note is too long.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            request.Status = approve ? "Approved" : "Denied";
            request.DecidedByUserId = userId;
            request.DecisionNote = String.IsNullOrWhiteSpace(decisionNote) ? null : decisionNote.Trim();
            request.DecidedAt = DateTime.UtcNow;
            if (approve)
            {
                task.DueDate = request.RequestedDueDate;
                task.UpdatedAt = DateTime.UtcNow;
                if (employee != null)
                {
                    task.Histories.Add(new TaskHistory
                    {
                        EmployeeId = employee.EmployeeId,
                        Action = "Deadline Extended",
                        Details = "Due date extended to " + request.RequestedDueDate.ToString("yyyy-MM-dd") + "."
                    });
                }
            }
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = approve ? "Task deadline extended." : "Deadline extension request denied.";
            return RedirectToAction("TaskDetails", new
            {
                id
            });
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult SetTaskPaused(int id, bool paused)
        {
            var task = db.Tasks.Find(id);
            if (task == null)
            {
                return HttpNotFound();
            }

            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId);
            if (paused && (task.Status == "Confirmed" || task.Status == "In Progress"))
            {
                task.Status = "On Hold";
            }
            else if (!paused && task.Status == "On Hold")
            {
                task.Status = task.Progress > 0 ? "In Progress" : "Confirmed";
            }
            else
            {
                TempData["WorkspaceError"] = "This task cannot be paused or resumed in its current status.";
                return RedirectToAction("TaskDetails", new
                {
                    id
                });
            }
            task.UpdatedAt = DateTime.UtcNow;
            if (employee != null)
            {
                task.Histories.Add(new TaskHistory
                {
                    EmployeeId = employee.EmployeeId,
                    Action = paused ? "Paused" : "Resumed",
                    Details = paused ? "Task paused by its owner." : "Task resumed by its owner."
                });
            }

            db.SaveChanges();
            TempData["WorkspaceSuccess"] = paused ? "Task paused." : "Task resumed.";
            return RedirectToAction("TaskDetails", new
            {
                id
            });
        }

        [HttpGet]
        public ActionResult DownloadJobAttachment(int id)
        {
            var attachment = db.JobAttachments.Include(file => file.Task).Include(file => file.Task.Project)
                .FirstOrDefault(file => file.JobAttachmentId == id);
            if (attachment == null)
            {
                return HttpNotFound();
            }

            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId);
            if (!RoleAccess.CanViewTask(User, employee, attachment.Task))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var safeStoredName = Path.GetFileName(attachment.StoredFileName);
            if (!String.Equals(safeStoredName, attachment.StoredFileName, StringComparison.Ordinal))
            {
                return HttpNotFound();
            }

            var path = Path.Combine(Server.MapPath("~/App_Data/JobAttachments"), safeStoredName);
            if (!System.IO.File.Exists(path))
            {
                return HttpNotFound();
            }

            return File(path, attachment.MimeType, attachment.FileName);
        }

        internal static string ValidateJobFile(HttpPostedFileBase file, out string mimeType)
        {
            mimeType = null;
            if (file.ContentLength <= 0 || file.ContentLength > 10 * 1024 * 1024)
            {
                return "Each attachment must be between 1 byte and 10 MB.";
            }

            var extension = Path.GetExtension(Path.GetFileName(file.FileName));
            var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".pdf", "application/pdf" },
                { ".doc", "application/msword" },
                { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { ".xls", "application/vnd.ms-excel" },
                { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
            };
            if (String.IsNullOrWhiteSpace(extension) || !allowed.TryGetValue(extension, out mimeType)
                || !String.Equals(file.ContentType, mimeType, StringComparison.OrdinalIgnoreCase))
            {
                return "Unsupported attachment. Upload a valid DOC, DOCX, XLS, XLSX, or PDF file.";
            }

            var stream = file.InputStream;
            if (!stream.CanSeek)
            {
                return "The uploaded file could not be validated.";
            }

            var originalPosition = stream.Position;
            try
            {
                stream.Position = 0;
                if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var header = new byte[5];
                    return stream.Read(header, 0, header.Length) == header.Length && Encoding.ASCII.GetString(header) == "%PDF-"
                        ? null
                        : "The file content does not match its PDF extension.";
                }
                if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    var header = new byte[8];
                    var expected = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
                    return stream.Read(header, 0, header.Length) == header.Length && header.SequenceEqual(expected)
                        ? null
                        : "The file content does not match its Office document extension.";
                }
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    var hasContentTypes = archive.GetEntry("[Content_Types].xml") != null;
                    var expectedEntry = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ? "word/document.xml" : "xl/workbook.xml";
                    return hasContentTypes && archive.GetEntry(expectedEntry) != null
                        ? null
                        : "The file content does not match its Office document extension.";
                }
            }
            catch (InvalidDataException) { return "The uploaded Office document is invalid."; }
            finally { stream.Position = originalPosition; }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateTask(int id, string status, int progress)
        {
            if (progress < 0 || progress > 100)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            var task = db.Tasks.Find(id);
            if (task == null)
            {
                return HttpNotFound();
            }

            if (task.ProjectId.HasValue
                && !db.Projects.Any(project => project.ProjectId == task.ProjectId.Value && project.Status == "In Progress"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Conflict, "The project is not active.");
            }

            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(e => e.UserId == userId);
            if (!RoleAccess.IsAdministrator(User) && (employee == null || task.AssignedEmployeeId != employee.EmployeeId))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var actor = employee ?? (RoleAccess.IsAdministrator(User) ? db.Employees.Find(task.CreatedByEmployeeId) : null);
            if (actor == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var allowed = new[] { "Not Started", "In Progress", "Completed", "Blocked", "Cancelled" };
            var transitions = new Dictionary<string, string[]> {
                { "Not Started", new[] { "Not Started", "In Progress", "Cancelled" } },
                { "In Progress", new[] { "In Progress", "Completed", "Blocked", "Cancelled" } },
                { "Blocked", new[] { "Blocked", "In Progress", "Cancelled" } },
                { "Completed", new[] { "Completed" } }, { "Cancelled", new[] { "Cancelled" } }
            };
            if (!allowed.Contains(status) || !transitions.ContainsKey(task.Status) || !transitions[task.Status].Contains(status))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            var previousStatus = task.Status;
            var previousProgress = task.Progress;
            if (progress == 100)
            {
                status = "Completed";
            }

            if (status == "Completed")
            {
                progress = 100;
            }

            if (status == "Not Started" && progress != 0)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            task.Status = status;
            task.Progress = progress;
            task.CompletedDate = status == "Completed" ? (task.CompletedDate ?? DateTime.Now) : (DateTime?)null;
            task.UpdatedAt = DateTime.Now;
            if (previousStatus != task.Status || previousProgress != task.Progress)
            {
                task.Histories.Add(new TaskHistory
                {
                    TaskId = id,
                    EmployeeId = actor.EmployeeId,
                    Action = "Updated",
                    Details = (RoleAccess.IsAdministrator(User) && employee == null ? "Admin " + User.Identity.Name + ". " : "") + "Status: " + previousStatus + " → " + task.Status + "; Progress: " + previousProgress + "% → " + task.Progress + "%"
                });
            }

            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Task updated.";
            return RedirectToAction("TaskDetails", "Task", new
            {
                id = id
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddTaskComment(int id, string comment)
        {
            if (String.IsNullOrWhiteSpace(comment) || comment.Trim().Length > 2000)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Enter a comment of up to 2,000 characters.");
            }

            var task = db.Tasks.Find(id);
            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(e => e.UserId == userId);
            if (task == null)
            {
                return HttpNotFound();
            }

            if (employee == null || (!RoleAccess.IsAdministrator(User) && task.AssignedEmployeeId != employee.EmployeeId))
            {
                if (!RoleAccess.IsAdministrator(User) || task == null)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
                }

                employee = db.Employees.Find(task.CreatedByEmployeeId);
                if (employee == null)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
                }
            }
            db.TaskComments.Add(new TaskComment { TaskId = id, EmployeeId = employee.EmployeeId, Comment = comment.Trim() });
            task.Histories.Add(new TaskHistory { TaskId = id, EmployeeId = employee.EmployeeId, Action = "Comment", Details = "Comment added." });
            task.UpdatedAt = DateTime.Now;
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Comment added.";
            return RedirectToAction("TaskDetails", "Task", new
            {
                id = id
            });
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeleteTask(int id)
        {
            var task = db.Tasks.Find(id);
            if (task == null)
            {
                return HttpNotFound();
            }

            var projectId = task.ProjectId;
            var attachmentPaths = TaskRecordCleanup.RemoveRelatedRecords(db, new[] { id });
            db.Tasks.Remove(task);
            db.SaveChanges();
            var directory = Server.MapPath("~/App_Data/JobAttachments");
            foreach (var storedName in attachmentPaths)
            {
                var safeName = Path.GetFileName(storedName);
                var path = Path.Combine(directory, safeName);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            TempData["WorkspaceSuccess"] = "Task and its task-specific records were deleted.";
            return RedirectToAction(projectId.HasValue ? "Projects" : "AdminTasks", "Home");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeleteTaskComment(int id)
        {
            var comment = db.TaskComments.Find(id);
            if (comment == null)
            {
                return HttpNotFound();
            }

            db.TaskComments.Remove(comment);
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Task comment deleted.";
            return RedirectToAction("TaskDetails", new
            {
                id = comment.TaskId
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
