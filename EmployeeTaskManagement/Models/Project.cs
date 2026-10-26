using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class Project
    {
        public int ProjectId { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; }

        [StringLength(2000)]
        public string Description { get; set; }

        public DateTime? DueDate { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; }

        [StringLength(20)]
        public string StatusBeforePause { get; set; }

        [Required]
        public int ProjectManagerEmployeeId { get; set; }
        public virtual Employee ProjectManager { get; set; }

        [Required, StringLength(128)]
        public string CreatedByUserId { get; set; }
        public virtual ApplicationUser CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }
        public virtual ICollection<ProjectMember> Members { get; set; }
        public virtual ICollection<Task> Tasks { get; set; }
        public virtual ICollection<ProjectProgressReport> ProgressReports { get; set; }
        public virtual ICollection<ProjectComment> Comments { get; set; }

        public Project()
        {
            CreatedAt = DateTime.UtcNow;
            Status = "Not Started";
            Members = new HashSet<ProjectMember>();
            Tasks = new HashSet<Task>();
            ProgressReports = new HashSet<ProjectProgressReport>();
            Comments = new HashSet<ProjectComment>();
        }
    }

    public class ProjectComment
    {
        public int ProjectCommentId { get; set; }
        [Required]
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; }
        [Required, StringLength(128)]
        public string AuthorUserId { get; set; }
        public virtual ApplicationUser Author { get; set; }
        public int? EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }
        public int? ParentCommentId { get; set; }
        public virtual ProjectComment ParentComment { get; set; }
        public virtual ICollection<ProjectComment> Replies { get; set; }
        [Required, StringLength(2000)]
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public ProjectComment()
        {
            Replies = new HashSet<ProjectComment>();
            CreatedAt = DateTime.UtcNow;
        }
    }

    public class TaskDeadlineRequest
    {
        public int TaskDeadlineRequestId { get; set; }
        [Required]
        public int TaskId { get; set; }
        public virtual Task Task { get; set; }
        [Required, StringLength(128)]
        public string RequestedByUserId { get; set; }
        public virtual ApplicationUser RequestedBy { get; set; }
        [Required]
        public DateTime RequestedDueDate { get; set; }
        [Required, StringLength(1000)]
        public string Reason { get; set; }
        [Required, StringLength(20)]
        public string Status { get; set; }
        [StringLength(128)]
        public string DecidedByUserId { get; set; }
        public virtual ApplicationUser DecidedBy { get; set; }
        [StringLength(1000)]
        public string DecisionNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DecidedAt { get; set; }

        public TaskDeadlineRequest()
        {
            Status = "Pending";
            CreatedAt = DateTime.UtcNow;
        }
    }

    public class ProjectMember
    {
        public int ProjectMemberId { get; set; }
        [Required]
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; }
        [Required]
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }
        [Required, StringLength(128)]
        public string AddedByUserId { get; set; }
        public DateTime AddedAt { get; set; }
        public ProjectMember()
        {
            AddedAt = DateTime.UtcNow;
        }
    }

    public class ProjectProgressReport
    {
        public int ProjectProgressReportId { get; set; }
        [Required]
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; }
        public int? TaskId { get; set; }
        public virtual Task Task { get; set; }
        [Required, StringLength(128)]
        public string ReporterUserId { get; set; }
        public virtual ApplicationUser Reporter { get; set; }
        [Required]
        public int RecipientEmployeeId { get; set; }
        public virtual Employee Recipient { get; set; }
        public bool? TaskCompleted { get; set; }
        [Required, StringLength(20)]
        public string ReportType { get; set; }
        [StringLength(2000)]
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual ICollection<ProjectReportAttachment> Attachments { get; set; }
        public ProjectProgressReport()
        {
            CreatedAt = DateTime.UtcNow;
            Attachments = new HashSet<ProjectReportAttachment>();
        }
    }

    public class ProjectReportAttachment
    {
        public int ProjectReportAttachmentId { get; set; }
        [Required]
        public int ProjectProgressReportId { get; set; }
        public virtual ProjectProgressReport Report { get; set; }
        [Required, StringLength(255)]
        public string FileName { get; set; }
        [Required, StringLength(100)]
        public string StoredFileName { get; set; }
        [Required, StringLength(150)]
        public string MimeType { get; set; }
        [Range(1, 10485760)]
        public long SizeBytes { get; set; }
        [Required, StringLength(128)]
        public string UploadedByUserId { get; set; }
        public virtual ApplicationUser UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        public ProjectReportAttachment()
        {
            UploadedAt = DateTime.UtcNow;
        }
    }
}
