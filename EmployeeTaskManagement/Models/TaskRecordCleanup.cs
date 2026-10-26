using System.Collections.Generic;
using System.Linq;

namespace EmployeeTaskManagement.Models
{
    internal static class TaskRecordCleanup
    {
        // The caller saves these changes together with removal of the task or project.
        public static IList<string> RemoveRelatedRecords(ApplicationDbContext db, ICollection<int> taskIds)
        {
            var attachments = db.JobAttachments.Where(file => taskIds.Contains(file.TaskId));
            var storedNames = attachments.Select(file => file.StoredFileName).ToList();

            foreach (var report in db.ProjectProgressReports.Where(report => report.TaskId.HasValue && taskIds.Contains(report.TaskId.Value)))
            {
                report.TaskId = null;
            }

            db.JobAttachments.RemoveRange(attachments);
            db.TaskComments.RemoveRange(db.TaskComments.Where(comment => taskIds.Contains(comment.TaskId)));
            db.TaskHistories.RemoveRange(db.TaskHistories.Where(history => taskIds.Contains(history.TaskId)));
            db.JobProgressLogs.RemoveRange(db.JobProgressLogs.Where(log => taskIds.Contains(log.TaskId)));
            db.TaskDeadlineRequests.RemoveRange(db.TaskDeadlineRequests.Where(request => taskIds.Contains(request.TaskId)));

            return storedNames;
        }
    }
}
