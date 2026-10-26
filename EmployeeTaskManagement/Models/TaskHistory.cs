using System;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class TaskHistory
    {
        public int TaskHistoryId { get; set; }

        [Required]
        public int TaskId { get; set; }

        public virtual Task Task { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        public virtual Employee Employee { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        [StringLength(1000)]
        public string Details { get; set; }

        public DateTime CreatedAt { get; set; }

        public TaskHistory()
        {
            CreatedAt = DateTime.Now;
        }
    }
}
