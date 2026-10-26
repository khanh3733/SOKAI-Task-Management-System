using System;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class JobProgressLog
    {
        public int JobProgressLogId { get; set; }

        [Required]
        public int TaskId { get; set; }
        public virtual Task Task { get; set; }

        [Required, StringLength(128)]
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        [Required, StringLength(30)]
        public string Status { get; set; }

        [Range(0, 100)]
        public int Percent { get; set; }

        [StringLength(2000)]
        public string Note { get; set; }

        public DateTime CreatedAt { get; set; }

        public JobProgressLog()
        {
            CreatedAt = DateTime.UtcNow;
        }
    }
}
