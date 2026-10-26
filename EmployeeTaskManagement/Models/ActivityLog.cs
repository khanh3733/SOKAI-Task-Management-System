using System;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class ActivityLog
    {
        public int ActivityLogId { get; set; }
        [StringLength(128)]
        public string UserId { get; set; }
        [Required, StringLength(256)]
        public string UserName { get; set; }
        [Required, StringLength(100)]
        public string Controller { get; set; }
        [Required, StringLength(100)]
        public string Action { get; set; }
        [Required, StringLength(10)]
        public string HttpMethod { get; set; }
        [Required, StringLength(20)]
        public string Result { get; set; }
        [StringLength(45)]
        public string IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }

        public ActivityLog()
        {
            CreatedAt = DateTime.UtcNow;
        }
    }
}
