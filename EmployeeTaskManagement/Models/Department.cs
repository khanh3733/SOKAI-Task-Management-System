using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class Department
    {
        public int DepartmentId { get; set; }

        [Required]
        [StringLength(100)]
        public string DepartmentName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public virtual ICollection<Employee> Employees { get; set; }

        public Department()
        {
            Employees = new HashSet<Employee>();
        }
    }
}
