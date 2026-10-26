using System;
using System.Linq;
using System.Web.Mvc;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity;

namespace EmployeeTaskManagement.Controllers
{
    public partial class HomeController
    {
        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult SaveDepartment(int? id, string departmentName, string description)
        {
            if (!ModelState.IsValid || String.IsNullOrWhiteSpace(departmentName) || departmentName.Trim().Length > 100
                || (description ?? "").Length > 500)
            {
                TempData["WorkspaceError"] = "Enter a department name (up to 100 characters) and a description of up to 500 characters.";
                return RedirectToAction("Departments");
            }
            var department = id.HasValue ? db.Departments.Find(id.Value) : new Department();
            if (department == null)
            {
                return HttpNotFound();
            }

            department.DepartmentName = departmentName.Trim();
            department.Description = String.IsNullOrWhiteSpace(description) ? null : description.Trim();
            if (!id.HasValue)
            {
                db.Departments.Add(department);
            }

            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Department saved.";
            return RedirectToAction("Departments");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeleteDepartment(int id)
        {
            var department = db.Departments.Find(id);
            if (department == null)
            {
                return HttpNotFound();
            }

            if (db.Employees.Any(e => e.DepartmentId == id))
            {
                TempData["WorkspaceError"] = "This department cannot be deleted while employees are assigned to it.";
                return RedirectToAction("Departments");
            }
            db.Departments.Remove(department);
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Department deleted.";
            return RedirectToAction("Departments");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult SaveEmployee(
            int? id,
            string userId,
            string fullName,
            string email,
            string phoneNumber,
            string position,
            DateTime hireDate,
            int departmentId)
        {
            var employee = id.HasValue ? db.Employees.Find(id.Value) : null;
            var identityUser = employee == null ? db.Users.Find(userId) : employee.User;
            var department = db.Departments.Find(departmentId);
            var employeeEmail = employee == null ? email : employee.Email;
            if (!ModelState.IsValid || (id.HasValue && employee == null) || identityUser == null || department == null
                || String.IsNullOrWhiteSpace(fullName)
                || fullName.Trim().Length > 100
                || String.IsNullOrWhiteSpace(employeeEmail)
                || employeeEmail.Trim().Length > 150
                || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(employeeEmail)
                || (phoneNumber ?? "").Length > 30
                || (position ?? "").Length > 100
                || hireDate.Date > DateTime.Today
                || db.Employees.Any(e => e.EmployeeId != (id ?? 0) && (e.UserId == identityUser.Id || e.Email == employeeEmail.Trim())))
            {
                TempData["WorkspaceError"] = "Check the employee details, selected account, department, and duplicate email.";
                return RedirectToAction("Employees");
            }
            if (employee == null)
            {
                employee = new Employee { UserId = identityUser.Id, Email = employeeEmail.Trim(), IsActive = true };
                db.Employees.Add(employee);
            }
            employee.FullName = fullName.Trim();
            employee.PhoneNumber = phoneNumber;
            employee.Position = position;
            employee.HireDate = hireDate.Date;
            employee.DepartmentId = departmentId;
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = id.HasValue ? "Employee updated." : "Employee created.";
            return RedirectToAction("Employees");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeactivateEmployee(int id)
        {
            var employee = db.Employees.Find(id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            employee.IsActive = false;
            db.SaveChanges();
            TempData["WorkspaceSuccess"] = "Employee deactivated. Historical task records were preserved.";
            return RedirectToAction("Employees");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeleteEmployee(int id)
        {
            var employee = db.Employees.Find(id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.UserId == User.Identity.GetUserId())
            {
                TempData["WorkspaceError"] = "You cannot delete your own employee account.";
                return RedirectToAction("Employees");
            }
            var inUse = db.Tasks.Any(task => task.AssignedEmployeeId == id || task.CreatedByEmployeeId == id)
                || db.TaskHistories.Any(history => history.EmployeeId == id)
                    || db.TaskComments.Any(comment => comment.EmployeeId == id)
                    || db.Projects.Any(project => project.ProjectManagerEmployeeId == id)
                    || db.ProjectMembers.Any(member => member.EmployeeId == id)
                    || db.ProjectProgressReports.Any(report => report.RecipientEmployeeId == id)
                    || db.ProjectComments.Any(comment => comment.EmployeeId == id || comment.AuthorUserId == employee.UserId)
                    || db.JobAttachments.Any(file => file.UploadedByUserId == employee.UserId)
                    || db.JobProgressLogs.Any(log => log.UserId == employee.UserId)
                    || db.ProjectReportAttachments.Any(file => file.UploadedByUserId == employee.UserId)
                    || db.ProjectProgressReports.Any(report => report.ReporterUserId == employee.UserId)
                    || db.TaskDeadlineRequests.Any(request => request.RequestedByUserId == employee.UserId || request.DecidedByUserId == employee.UserId)
                    || db.Projects.Any(project => project.CreatedByUserId == employee.UserId)
                    || db.ProjectMembers.Any(member => member.AddedByUserId == employee.UserId);
            if (inUse)
            {
                TempData["WorkspaceError"] = "This employee is referenced by projects or work history. Deactivate the employee to preserve those records.";
                return RedirectToAction("Employees");
            }
            var identityUser = db.Users.Find(employee.UserId);
            var adminRoleId = db.Roles.Where(role => role.Name == "Admin").Select(role => role.Id).FirstOrDefault();
            if (identityUser != null && identityUser.Roles.Any(role => role.RoleId == adminRoleId)
                && db.Users.Count(candidate => candidate.Roles.Any(role => role.RoleId == adminRoleId)
                && (!candidate.LockoutEndDateUtc.HasValue || candidate.LockoutEndDateUtc.Value <= DateTime.UtcNow)) <= 1)
            {
                TempData["WorkspaceError"] = "The last available Administrator account cannot be deleted.";
                return RedirectToAction("Employees");
            }
            using (var transaction = db.Database.BeginTransaction())
            {
                db.Employees.Remove(employee);
                db.SaveChanges();
                if (identityUser != null)
                {
                    var userManager = ApplicationUserManager.CreateForSeeding(db);
                    var deleteResult = userManager.DeleteAsync(identityUser).GetAwaiter().GetResult();
                    if (!deleteResult.Succeeded)
                    {
                        transaction.Rollback();
                        TempData["WorkspaceError"] = String.Join(" ", deleteResult.Errors);
                        return RedirectToAction("Employees");
                    }
                }
                transaction.Commit();
            }
            TempData["WorkspaceSuccess"] = "Employee and linked account deleted.";
            return RedirectToAction("Employees");
        }
    }
}
