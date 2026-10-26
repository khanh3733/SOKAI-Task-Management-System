using System.Security.Principal;
using System.Linq;
using EmployeeTaskManagement.Models;

namespace EmployeeTaskManagement.Controllers
{
    internal static class RoleAccess
    {
        public static bool IsAdministrator(IPrincipal user)
        {
            return user != null && user.IsInRole("Admin");
        }

        public static bool ManagesProject(IPrincipal user, Employee employee, Project project)
        {
            return user != null && user.IsInRole("Manager")
                && employee != null && employee.IsActive && project != null
                && project.ProjectManagerEmployeeId == employee.EmployeeId;
        }

        public static bool CanAssignTask(
            IPrincipal user, Employee employee, Project project, Employee assignee, bool assigneeHasUserRole)
        {
            if (assignee == null || !assignee.IsActive)
            {
                return false;
            }

            if (project != null && (project.Status != "In Progress"
                || !project.Members.Any(member => member.EmployeeId == assignee.EmployeeId)))
            {
                return false;
            }

            return IsAdministrator(user)
                || (ManagesProject(user, employee, project) && assigneeHasUserRole
                    && assignee.EmployeeId != employee.EmployeeId);
        }

        public static bool CanViewTask(IPrincipal user, Employee employee, Task task)
        {
            return task != null && (IsAdministrator(user)
                || (employee != null && employee.IsActive
                    && (task.AssignedEmployeeId == employee.EmployeeId
                        || ManagesProject(user, employee, task.Project))));
        }

        public static IQueryable<Project> ManagedProjects(IPrincipal user, Employee employee, IQueryable<Project> projects)
        {
            if (user == null || !user.IsInRole("Manager") || employee == null || !employee.IsActive)
            {
                return projects.Where(project => false);
            }

            return projects.Where(project => project.ProjectManagerEmployeeId == employee.EmployeeId);
        }

        public static IQueryable<Task> ManagedTasks(IPrincipal user, Employee employee, IQueryable<Task> tasks)
        {
            if (user == null || !user.IsInRole("Manager") || employee == null || !employee.IsActive)
            {
                return tasks.Where(task => false);
            }

            return tasks.Where(task => task.ProjectId.HasValue && task.Project.ProjectManagerEmployeeId == employee.EmployeeId);
        }
    }
}
