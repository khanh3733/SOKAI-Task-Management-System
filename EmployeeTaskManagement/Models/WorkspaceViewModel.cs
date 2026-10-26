using System.Collections.Generic;

namespace EmployeeTaskManagement.Models
{
    public class WorkspaceViewModel
    {
        public string Page { get; set; }

        public string CurrentUserName { get; set; }

        public string CurrentUserEmail { get; set; }

        public Employee CurrentEmployee { get; set; }

        public IList<Task> Tasks { get; set; }

        public IList<Employee> Employees { get; set; }

        public IList<Project> Projects { get; set; }

        public IList<ProjectProgressReport> ProjectReports { get; set; }

        public IList<Department> Departments { get; set; }

        public IList<ApplicationUser> IdentityUsers { get; set; }

        public IList<UserAccountListItem> UserAccounts { get; set; }

        public IList<string> AvailableRoles { get; set; }

        public IList<ActivityLog> ActivityLogs { get; set; }

        public int TotalEmployees { get; set; }

        public int TotalTasks { get; set; }

        public int CompletedTasks { get; set; }

        public int InProgressTasks { get; set; }

        public int NotStartedTasks { get; set; }

        public int OverdueTasks { get; set; }

        public int AverageProgress { get; set; }

        public WorkspaceViewModel()
        {
            Tasks = new List<Task>();
            Employees = new List<Employee>();
            Projects = new List<Project>();
            ProjectReports = new List<ProjectProgressReport>();
            Departments = new List<Department>();
            IdentityUsers = new List<ApplicationUser>();
            UserAccounts = new List<UserAccountListItem>();
            AvailableRoles = new List<string>();
            ActivityLogs = new List<ActivityLog>();
        }
    }
}
