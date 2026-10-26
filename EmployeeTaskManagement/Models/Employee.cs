using System;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class Employee
    {
        public Employee()
        {
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
        }

        public int EmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [StringLength(150)]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(30)]
        public string PhoneNumber { get; set; }

        [StringLength(100)]
        public string Position { get; set; }

        public DateTime HireDate { get; set; }

        public bool IsActive { get; set; }

        public EmployeeGender? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        [StringLength(500)]
        public string Address { get; set; }

        [StringLength(260)]
        public string AvatarPath { get; set; }

        [StringLength(128)]
        public string CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        // Identity User
        [Required]
        public string UserId { get; set; }

        public virtual ApplicationUser User { get; set; }

        // Department
        public int? DepartmentId { get; set; }

        public virtual Department Department { get; set; }
    }
}
