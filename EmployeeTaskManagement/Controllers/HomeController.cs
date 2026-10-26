using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace EmployeeTaskManagement.Controllers
{
    [Authorize]
    public partial class HomeController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        public ActionResult Index()
        {
            if (RoleAccess.IsAdministrator(User))
            {
                return RedirectToAction("Admin");
            }

            return View(BuildWorkspace("My Tasks"));
        }

        public ActionResult Pages()
        {
            return RoleAccess.IsAdministrator(User) ? Page("Admin Dashboard") : Page("My Tasks");
        }

        public ActionResult Tasks()
        {
            return Page("My Tasks");
        }

        [Authorize(Roles = "Manager")]
        public ActionResult ManagerProgress()
        {
            return Page("Project progress");
        }

        [Authorize(Roles = "Admin")]
        public ActionResult AdminTasks()
        {
            return Page("Task Management");
        }

        public new ActionResult Profile()
        {
            return Page("Profile");
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Admin()
        {
            return Page("Admin Dashboard");
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Employees()
        {
            return Page("Employees");
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Departments()
        {
            return Page("Departments");
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Reports()
        {
            return Page("Reports");
        }

        [Authorize(Roles = "Admin")]
        public ActionResult AuditLog()
        {
            return Page("Audit Logs");
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Projects()
        {
            return Page("Projects");
        }

        public ActionResult Notifications()
        {
            return Page("Notifications");
        }

        private ActionResult Page(string page)
        {
            ViewBag.Title = page;
            ViewBag.Page = page;
            ViewBag.ContentView = PageView(page);
            return View("Workspace", BuildWorkspace(page));
        }

        private static string PageView(string page)
        {
            switch (page)
            {
                case "My Tasks":
                    return "_TasksPage";
                case "Project progress":
                    return "_ManagerProgressPage";
                case "Task Management":
                    return "_TasksPage";
                case "Profile":
                    return "_ProfilePage";
                case "Admin Dashboard":
                    return "_AdminDashboardPage";
                case "Employees":
                    return "_EmployeesPage";
                case "Departments":
                    return "_DepartmentsPage";
                case "Projects":
                    return "_ProjectsPage";
                case "Reports":
                    return "_ReportsPage";
                case "Audit Logs":
                    return "_AuditLogPage";
                case "Notifications":
                    return "_NotificationsPage";
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown workspace page.");
            }
        }

        private WorkspaceViewModel BuildWorkspace(string page)
        {
            var userId = User.Identity.GetUserId();
            var user = db.Users.Find(userId);
            var employee = db.Employees.Include(e => e.Department).FirstOrDefault(e => e.UserId == userId && e.IsActive);
            var currentEmployeeId = employee == null ? 0 : employee.EmployeeId;
            var isAdmin = RoleAccess.IsAdministrator(User);
            var model = new WorkspaceViewModel
            {
                Page = page,
                CurrentUserName = employee == null ? (user == null ? User.Identity.Name : user.UserName) : employee.FullName,
                CurrentUserEmail = user == null ? User.Identity.Name : user.Email,
                CurrentEmployee = employee
            };
            if (isAdmin)
            {
                model.TotalEmployees = db.Employees.Count();
            }
            if (page == "Audit Logs")
            {
                ActivityLogSchema.EnsureCreated(db);
                model.ActivityLogs = db.Database.SqlQuery<ActivityLog>("SELECT TOP (1000) ActivityLogId, UserId, UserName, Controller, Action, HttpMethod, Result, IpAddress, CreatedAt FROM dbo.ActivityLogs ORDER BY CreatedAt DESC")
                    .ToList();
            }

            if (page == "Employees")
            {
                model.Employees = db.Employees.Include(e => e.Department).OrderBy(e => e.FullName).ToList();
                model.Departments = db.Departments.OrderBy(d => d.DepartmentName).ToList();
                model.IdentityUsers = db.Users.OrderBy(u => u.UserName).ToList();
                model.AvailableRoles = db.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToList();
                model.UserAccounts = BuildUserAccountRows(model.IdentityUsers, model.Employees);
            }

            if (page == "Departments" || page == "Reports")
            {
                model.Departments = db.Departments.Include(d => d.Employees).OrderBy(d => d.DepartmentName).ToList();
            }

            if (page == "My Tasks" || page == "Admin Dashboard" || page == "Task Management" || page == "Reports" || page == "Project progress")
            {
                var tasks = db.Tasks.Include(t => t.AssignedEmployee).Include(t => t.AssignedEmployee.Department).Include(t => t.CreatedByEmployee);
                if (page == "Project progress")
                {
                    model.Tasks = RoleAccess.ManagedTasks(User, employee, tasks)
                        .OrderByDescending(task => task.UpdatedAt).ToList();
                }
                else if (!isAdmin)
                {
                    model.Tasks = employee == null
                        ? new System.Collections.Generic.List<EmployeeTaskManagement.Models.Task>()
                        : tasks.Where(t => t.AssignedEmployeeId == employee.EmployeeId).OrderByDescending(t => t.UpdatedAt).ToList();
                }
                else
                {
                    model.Tasks = tasks.OrderByDescending(t => t.UpdatedAt).ToList();
                }

                model.TotalTasks = model.Tasks.Count;
                model.CompletedTasks = model.Tasks.Count(t => t.Status == "Completed");
                model.InProgressTasks = model.Tasks.Count(t => t.Status == "In Progress");
                model.NotStartedTasks = model.Tasks.Count(t => t.Status == "Not Started" || t.Status == "Pending" || t.Status == "Confirmed");
                model.OverdueTasks = model.Tasks.Count(t => t.DueDate.HasValue && t.DueDate < DateTime.Today && t.Status != "Completed" && t.Status != "Cancelled");
                model.AverageProgress = model.Tasks.Any() ? (int)Math.Round(model.Tasks.Average(t => (double)t.Progress)) : 0;
            }

            if (page == "Task Management")
            {
                model.Employees = db.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToList();
            }

            if (page == "Projects" || page == "My Tasks" || page == "Task Management" || page == "Reports" || page == "Admin Dashboard" || page == "Project progress")
            {
                var projects = db.Projects.Include(project => project.ProjectManager)
                    .Include(project => project.Members.Select(member => member.Employee))
                    .Include(project => project.Tasks.Select(task => task.AssignedEmployee))
                    .Include(project => project.Comments.Select(comment => comment.Employee))
                    .Include(project => project.Comments.Select(comment => comment.Author))
                    .Include(project => project.Comments.Select(comment => comment.Replies.Select(reply => reply.Employee)))
                    .Include(project => project.Comments.Select(comment => comment.Replies.Select(reply => reply.Author)));
                if (page == "Project progress")
                {
                    projects = RoleAccess.ManagedProjects(User, employee, projects);
                }
                model.Projects = isAdmin
                    ? projects.OrderBy(project => project.Name).ToList()
                    : employee == null
                    ? new List<Project>()
                    : projects.Where(project => project.ProjectManagerEmployeeId == employee.EmployeeId
                    || project.Members.Any(member => member.EmployeeId == employee.EmployeeId))
                    .OrderBy(project => project.Name)
                    .ToList();
                var reportQuery = db.ProjectProgressReports.Include(report => report.Project).Include(report => report.Task)
                    .Include(report => report.Reporter)
                    .Include(report => report.Recipient)
                    .Include(report => report.Attachments)
                    .Where(report => isAdmin || report.ReporterUserId == userId || report.RecipientEmployeeId == currentEmployeeId
                    || report.Project.ProjectManagerEmployeeId == currentEmployeeId)
                    .OrderByDescending(report => report.CreatedAt)
                    .Take(100)
                    .ToList();
                model.ProjectReports = reportQuery;
            }

            if (page == "Projects")
            {
                model.Employees = EmployeeEligibility.ProjectManagers(db).OrderBy(item => item.FullName).ToList();
            }
            else if (page == "My Tasks")
            {
                model.Employees = EmployeeEligibility.ProjectMembers(db).OrderBy(item => item.FullName).ToList();
            }

            return model;
        }

        private IList<UserAccountListItem> BuildUserAccountRows(IList<ApplicationUser> users, IList<Employee> employees)
        {
            var roleLookup = (from userRole in db.Set<IdentityUserRole>()
                              join role in db.Roles on userRole.RoleId equals role.Id
                              select new { userRole.UserId, role.Name })
                .ToList()
                .GroupBy(item => item.UserId)
                .ToDictionary(group => group.Key, group => String.Join(", ", group.Select(item => item.Name)));
            return users.Select(user => new UserAccountListItem
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Role = roleLookup.ContainsKey(user.Id) ? roleLookup[user.Id] : "Unassigned",
                IsLocked = user.LockoutEndDateUtc.HasValue && user.LockoutEndDateUtc.Value > DateTime.UtcNow,
                Employee = employees.FirstOrDefault(employee => employee.UserId == user.Id)
            })
                .ToList();
        }
    }
}
