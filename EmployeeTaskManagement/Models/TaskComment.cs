using System;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class TaskComment
    {
        public int TaskCommentId { get; set; }

        [Required]
        public int TaskId { get; set; }

        public virtual Task Task { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        public virtual Employee Employee { get; set; }

        [Required]
        [StringLength(2000)]
        public string Comment { get; set; }

        public DateTime CreatedAt { get; set; }

        public TaskComment()
        {
            CreatedAt = DateTime.Now;
        }
    }
}
