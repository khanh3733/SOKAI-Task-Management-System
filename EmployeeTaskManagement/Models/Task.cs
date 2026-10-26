using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class Task
    {
        public int TaskId { get; set; }

        public int? ProjectId { get; set; }
        public virtual Project Project { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(2000)]
        public string Description { get; set; }

        // Người được giao công việc
        [Required]
        public int AssignedEmployeeId { get; set; }

        public virtual Employee AssignedEmployee { get; set; }

        // Người tạo công việc
        [Required]
        public int CreatedByEmployeeId { get; set; }

        public virtual Employee CreatedByEmployee { get; set; }

        // Trạng thái
        [Required]
        [StringLength(30)]
        public string Status { get; set; }

        // Độ ưu tiên
        [Required]
        [StringLength(20)]
        public string Priority { get; set; }

        // Tiến độ 0 - 100
        [Range(0, 100)]
        public int Progress { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<TaskComment> Comments { get; set; }

        public virtual ICollection<TaskHistory> Histories { get; set; }

        public virtual ICollection<JobProgressLog> ProgressLogs { get; set; }

        public virtual ICollection<JobAttachment> Attachments { get; set; }

        public Task()
        {
            Comments = new HashSet<TaskComment>();
            Histories = new HashSet<TaskHistory>();
            ProgressLogs = new HashSet<JobProgressLog>();
            Attachments = new HashSet<JobAttachment>();

            Status = "Not Started";
            Priority = "Medium";
            Progress = 0;

            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }
    }
}
