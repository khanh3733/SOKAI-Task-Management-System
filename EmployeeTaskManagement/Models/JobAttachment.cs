using System;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class JobAttachment
    {
        public int JobAttachmentId { get; set; }

        [Required]
        public int TaskId { get; set; }
        public virtual Task Task { get; set; }

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

        public JobAttachment()
        {
            UploadedAt = DateTime.UtcNow;
        }
    }
}
