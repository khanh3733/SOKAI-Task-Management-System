using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity;

namespace EmployeeTaskManagement.Controllers
{
    public partial class HomeController
    {
        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeleteProject(int id)
        {
            var project = db.Projects.Find(id);
            if (project == null)
            {
                return HttpNotFound();
            }

            var taskIds = db.Tasks.Where(task => task.ProjectId == id).Select(task => task.TaskId).ToList();
            var jobFiles = TaskRecordCleanup.RemoveRelatedRecords(db, taskIds);

            db.Tasks.RemoveRange(db.Tasks.Where(task => task.ProjectId == id));
            var reportIds = db.ProjectProgressReports.Where(report => report.ProjectId == id).Select(report => report.ProjectProgressReportId)
                .ToList();
            var reportFiles = db.ProjectReportAttachments.Where(file => reportIds.Contains(file.ProjectProgressReportId))
                .Select(file => file.StoredFileName)
                .ToList();
            db.ProjectReportAttachments.RemoveRange(db.ProjectReportAttachments.Where(file => reportIds.Contains(file.ProjectProgressReportId)));
            db.ProjectProgressReports.RemoveRange(db.ProjectProgressReports.Where(report => report.ProjectId == id));
            db.ProjectComments.Where(comment => comment.ProjectId == id).ToList().ForEach(comment => comment.ParentCommentId = null);
            db.ProjectComments.RemoveRange(db.ProjectComments.Where(comment => comment.ProjectId == id));
            db.ProjectMembers.RemoveRange(db.ProjectMembers.Where(member => member.ProjectId == id));
            db.Projects.Remove(project);
            db.SaveChanges();
            foreach (var fileName in jobFiles)
            {
                var path = Path.Combine(Server.MapPath("~/App_Data/JobAttachments"), Path.GetFileName(fileName));
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }

            foreach (var fileName in reportFiles)
            {
                var path = Path.Combine(Server.MapPath("~/App_Data/ProjectReportAttachments"), Path.GetFileName(fileName));
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }

            TempData["WorkspaceSuccess"] = "Project and its project-specific records were deleted.";
            return RedirectToAction("Projects");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeleteProjectComment(int id)
        {
            var comment = db.ProjectComments.Find(id);
            if (comment == null)
            {
                return HttpNotFound();
            }

            db.ProjectComments.Where(reply => reply.ParentCommentId == id).ToList().ForEach(reply => reply.ParentCommentId = null);
            db.ProjectComments.Remove(comment);
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Project comment deleted.";
            return RedirectToAction(User.IsInRole("Admin") ? "Projects" : "Tasks", "Home");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult SaveProject(string name, string description, int projectManagerEmployeeId, DateTime? dueDate)
        {
            var manager = EmployeeEligibility.ProjectManagers(db).FirstOrDefault(employee => employee.EmployeeId == projectManagerEmployeeId);
            if (!ModelState.IsValid || String.IsNullOrWhiteSpace(name) || name.Trim().Length > 150 || (description ?? "").Length > 2000
                || manager == null
                || !dueDate.HasValue
                || dueDate.Value.Date < DateTime.Today)
            {
                TempData["WorkspaceError"] = "Enter a valid project name, active project manager, and deadline.";
                return RedirectToAction("Projects");
            }

            var userId = User.Identity.GetUserId();
            var project = new Project
            {
                Name = name.Trim(),
                Description = String.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                ProjectManagerEmployeeId = manager.EmployeeId,
                CreatedByUserId = userId,
                DueDate = dueDate
            };
            db.Projects.Add(project);
            project.Members.Add(new ProjectMember { EmployeeId = manager.EmployeeId, AddedByUserId = userId });
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Project created. The project manager can add members after you start it.";
            return RedirectToAction("Projects");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult SetProjectStatus(int id, string action)
        {
            var project = db.Projects.Include(item => item.Members).FirstOrDefault(item => item.ProjectId == id);
            if (project == null)
            {
                return HttpNotFound();
            }

            if (action == "start" && project.Status == "Not Started")
            {
                project.Status = "Ready";
            }
            else if (action == "pause" && (project.Status == "Ready" || project.Status == "In Progress"))
            {
                project.StatusBeforePause = project.Status;
                project.Status = "Paused";
            }
            else if (action == "resume" && project.Status == "Paused")
            {
                project.Status = project.StatusBeforePause == "In Progress" ? "In Progress" : "Ready";
                project.StatusBeforePause = null;
            }
            else
            {
                TempData["WorkspaceError"] = "This project action is not available in its current status.";
                return RedirectToAction("Projects");
            }

            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Project status updated.";
            return RedirectToAction("Projects");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult UpdateProjectDeadline(int id, DateTime? dueDate)
        {
            var project = db.Projects.Find(id);
            if (project == null)
            {
                return HttpNotFound();
            }

            if (!dueDate.HasValue || dueDate.Value.Date < DateTime.Today)
            {
                TempData["WorkspaceError"] = "The project deadline cannot be in the past.";
                return RedirectToAction("Projects");
            }

            project.DueDate = dueDate;
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Project deadline updated.";
            return RedirectToAction("Projects");
        }

        [HttpPost, Authorize(Roles = "Manager"), ValidateAntiForgeryToken]
        public ActionResult AddProjectMembers(int id, int[] employeeIds)
        {
            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId && item.IsActive);
            var project = db.Projects.Include(item => item.Members).FirstOrDefault(item => item.ProjectId == id);
            if (project == null)
            {
                return HttpNotFound();
            }

            if (!RoleAccess.ManagesProject(User, employee, project))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            // 実行開始前と実行中のどちらでも、プロジェクト責任者がメンバーを追加できるようにします。
            if (project.Status != "Ready" && project.Status != "In Progress")
            {
                TempData["WorkspaceError"] = "Members can only be added to projects that are ready or in progress.";
                return RedirectToAction("Tasks");
            }

            var requestedIds = (employeeIds ?? new int[0]).Distinct().ToList();
            var validIds = EmployeeEligibility.ProjectMembers(db).Where(item => requestedIds.Contains(item.EmployeeId))
                .Select(item => item.EmployeeId)
                .ToList();
            if (validIds.Count != requestedIds.Count)
            {
                TempData["WorkspaceError"] = "Choose active User accounts only.";
                return RedirectToAction("Tasks");
            }

            foreach (var employeeId in validIds.Where(idValue => !project.Members.Any(member => member.EmployeeId == idValue)))
            {
                project.Members.Add(new ProjectMember { EmployeeId = employeeId, AddedByUserId = userId });
            }

            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Project members updated.";
            return RedirectToAction("Tasks");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult StartProjectExecution(int id)
        {
            var project = db.Projects.Find(id);
            if (project == null)
            {
                return HttpNotFound();
            }

            if (project.Status != "Ready")
            {
                TempData["WorkspaceError"] = "Only a project started by an administrator can begin execution.";
                return RedirectToAction("Projects");
            }

            project.Status = "In Progress";
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Project execution started. You can now assign tasks.";
            return RedirectToAction("Projects");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddProjectComment(int id, int? parentCommentId, string content)
        {
            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId && item.IsActive);
            var project = db.Projects.Include(item => item.Members).FirstOrDefault(item => item.ProjectId == id);
            if (project == null)
            {
                return HttpNotFound();
            }

            if ((!RoleAccess.IsAdministrator(User) && employee == null)
                || (!RoleAccess.IsAdministrator(User) && project.ProjectManagerEmployeeId != employee.EmployeeId
                && !project.Members.Any(member => member.EmployeeId == employee.EmployeeId)))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            if (String.IsNullOrWhiteSpace(content) || content.Trim().Length > 2000)
            {
                TempData["WorkspaceError"] = "Enter a project comment of up to 2,000 characters.";
                return RedirectToAction(RoleAccess.IsAdministrator(User) ? "Projects" : "Tasks");
            }

            if (parentCommentId.HasValue
                && !db.ProjectComments.Any(comment => comment.ProjectCommentId == parentCommentId.Value && comment.ProjectId == id))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            db.ProjectComments.Add(new ProjectComment
            {
                ProjectId = id,
                AuthorUserId = userId,
                EmployeeId = employee == null ? (int?)null : employee.EmployeeId,
                ParentCommentId = parentCommentId,
                Content = content.Trim()
            });
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = parentCommentId.HasValue ? "Reply added." : "Project comment added.";
            return RedirectToAction(RoleAccess.IsAdministrator(User) ? "Projects" : "Tasks");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SubmitProjectReport(
            int projectId,
            int? taskId,
            int recipientEmployeeId,
            bool? taskCompleted,
            string progressStatus,
            int? percent,
            string note,
            IEnumerable<HttpPostedFileBase> attachments)
        {
            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId && item.IsActive);
            var project = db.Projects.Include(item => item.Members).FirstOrDefault(item => item.ProjectId == projectId);
            if (employee == null || project == null
                || (!RoleAccess.IsAdministrator(User) && project.ProjectManagerEmployeeId != employee.EmployeeId
                && !project.Members.Any(member => member.EmployeeId == employee.EmployeeId)))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var recipientIsMember = project.ProjectManagerEmployeeId == recipientEmployeeId
                || project.Members.Any(member => member.EmployeeId == recipientEmployeeId);
            var task = taskId.HasValue ? db.Tasks.FirstOrDefault(item => item.TaskId == taskId.Value && item.ProjectId == projectId) : null;
            var validProgressStatus = new[] { "not_started", "in_progress", "on_hold" };
            var validProgress = !taskId.HasValue
                || (taskCompleted == true
                || (taskCompleted == false && percent.HasValue && percent.Value >= 0 && percent.Value <= 99
                && ((progressStatus == "not_started" && percent.Value == 0) || (progressStatus == "in_progress" && percent.Value > 0)
                || (progressStatus == "on_hold" && percent.Value > 0))));
            if (!recipientIsMember
                || (taskId.HasValue
                && (task == null || (task.AssignedEmployeeId != employee.EmployeeId && !RoleAccess.IsAdministrator(User))))
                || (taskId.HasValue
                && (!taskCompleted.HasValue || !validProgress || project.Status != "In Progress"
                || (task.Status != "Confirmed" && task.Status != "In Progress" && task.Status != "On Hold")))
                || (taskId.HasValue && taskCompleted == false && String.IsNullOrWhiteSpace(note))
                || (!taskId.HasValue && String.IsNullOrWhiteSpace(note))
                || (note ?? "").Length > 2000)
            {
                TempData["WorkspaceError"] = "Select a project recipient and task, and include a comment when the task is not complete.";
                return RedirectToAction("Tasks");
            }

            var files = (attachments ?? Enumerable.Empty<HttpPostedFileBase>()).Where(file => file != null && file.ContentLength > 0).ToList();
            var validatedFiles = new List<Tuple<HttpPostedFileBase, string, string>>();
            foreach (var file in files)
            {
                string mime;
                var error = TaskController.ValidateJobFile(file, out mime);
                if (error != null)
                {
                    TempData["WorkspaceError"] = error;
                    return RedirectToAction("Tasks");
                }

                validatedFiles.Add(Tuple.Create(file, mime, Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant()));
            }

            if (taskId.HasValue && taskCompleted == false && !validProgressStatus.Contains(progressStatus))
            {
                TempData["WorkspaceError"] = "Choose a valid task progress status and percentage.";
                return RedirectToAction("Tasks");
            }

            var report = new ProjectProgressReport
            {
                ProjectId = projectId,
                TaskId = taskId,
                ReporterUserId = userId,
                RecipientEmployeeId = recipientEmployeeId,
                TaskCompleted = taskId.HasValue ? taskCompleted : (bool?)null,
                ReportType = taskId.HasValue ? "TaskUpdate" : "ProjectSuggestion",
                Note = String.IsNullOrWhiteSpace(note) ? null : note.Trim()
            };
            var paths = new List<string>();
            var storageDirectory = Server.MapPath("~/App_Data/ProjectReportAttachments");
            try
            {
                if (validatedFiles.Count > 0)
                {
                    Directory.CreateDirectory(storageDirectory);
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    db.ProjectProgressReports.Add(report);
                    foreach (var item in validatedFiles)
                    {
                        var file = item.Item1;
                        var storedName = Guid.NewGuid().ToString("N") + item.Item3;
                        var destination = Path.Combine(storageDirectory, storedName);
                        file.InputStream.Position = 0;
                        file.SaveAs(destination);
                        paths.Add(destination);
                        report.Attachments.Add(new ProjectReportAttachment
                        {
                            FileName = Path.GetFileName(file.FileName),
                            StoredFileName = storedName,
                            MimeType = item.Item2,
                            SizeBytes = file.ContentLength,
                            UploadedByUserId = userId
                        });
                    }

                    if (task != null)
                    {
                        task.Progress = taskCompleted == true ? 100 : percent.Value;
                        task.Status = taskCompleted == true
                            ? "Completed Pending Review"
                            : progressStatus == "on_hold" ? "On Hold" : progressStatus == "not_started" ? "Confirmed" : "In Progress";
                        task.CompletedDate = null;
                        task.UpdatedAt = DateTime.UtcNow;
                        var logStatus = taskCompleted == true ? "completed" : progressStatus;
                        db.JobProgressLogs.Add(new JobProgressLog
                        {
                            TaskId = task.TaskId,
                            UserId = userId,
                            Status = logStatus,
                            Percent = task.Progress,
                            Note = report.Note
                        });
                        task.Histories.Add(new TaskHistory
                        {
                            TaskId = task.TaskId,
                            EmployeeId = employee.EmployeeId,
                            Action = "Progress Report",
                            Details = logStatus + " — " + task.Progress + "%" + (taskCompleted == true ? "; completion submitted for project review." : "")
                        });
                    }

                    db.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (Exception exception)
            {
                foreach (var path in paths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }

                System.Diagnostics.Trace.TraceError("Project report save failed: {0}", exception);
                TempData["WorkspaceError"] = "The project report could not be saved. Try again or contact support.";
                return RedirectToAction("Tasks");
            }

            TempData["WorkspaceSuccess"] = "Project report sent.";
            return RedirectToAction("Tasks");
        }

        [HttpGet]
        public ActionResult DownloadProjectReportAttachment(int id)
        {
            var file = db.ProjectReportAttachments.Include(item => item.Report.Project).Include(item => item.Report.Project.Members)
                .FirstOrDefault(item => item.ProjectReportAttachmentId == id);
            if (file == null)
            {
                return HttpNotFound();
            }

            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId);
            var project = file.Report.Project;
            if (!RoleAccess.IsAdministrator(User)
                && (employee == null
                || (project.ProjectManagerEmployeeId != employee.EmployeeId
                && !project.Members.Any(member => member.EmployeeId == employee.EmployeeId)
                && file.Report.RecipientEmployeeId != employee.EmployeeId)))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var safeName = Path.GetFileName(file.StoredFileName);
            if (!String.Equals(safeName, file.StoredFileName, StringComparison.Ordinal))
            {
                return HttpNotFound();
            }

            var path = Path.Combine(Server.MapPath("~/App_Data/ProjectReportAttachments"), safeName);
            return System.IO.File.Exists(path) ? File(path, file.MimeType, file.FileName) : (ActionResult)HttpNotFound();
        }
    }
}
