using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace EmployeeTaskManagement.Controllers
{
    public partial class HomeController
    {
        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult CreateUserAccount(CreateUserAccountViewModel model)
        {
            if (model == null)
            {
                ModelState.AddModelError("", "Enter the account details and try again.");
                return UserAccountCreationFailure();
            }

            if (model.DateOfBirth.HasValue && model.DateOfBirth.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError("DateOfBirth", "Date of birth cannot be in the future.");
            }

            if (!User.IsInRole("Admin") && model.Role != "User")
            {
                ModelState.AddModelError("Role", "Managers can create User accounts only.");
            }

            if (model.DepartmentId.HasValue && !db.Departments.Any(department => department.DepartmentId == model.DepartmentId.Value))
            {
                ModelState.AddModelError("DepartmentId", "Select an existing department.");
            }

            if (!db.Roles.Any(role => role.Name == model.Role))
            {
                ModelState.AddModelError("Role", "Select an existing role.");
            }

            var normalizedUsername = (model.Username ?? "").Trim();
            var normalizedEmail = (model.Email ?? "").Trim();
            if (normalizedUsername.Length > 0 && db.Users.Any(user => user.UserName == normalizedUsername))
            {
                ModelState.AddModelError("Username", "This username is already in use.");
            }

            if (normalizedEmail.Length > 0 && db.Users.Any(user => user.Email == normalizedEmail))
            {
                ModelState.AddModelError("Email", "This email address is already in use.");
            }

            if (!ModelState.IsValid)
            {
                return UserAccountCreationFailure();
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                var userManager = ApplicationUserManager.CreateForSeeding(db);
                var user = new ApplicationUser
                {
                    UserName = model.Username.Trim(),
                    Email = model.Email.Trim(),
                    PhoneNumber = String.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim()
                };
                var createResult = userManager.CreateAsync(user, model.Password).GetAwaiter().GetResult();
                if (!createResult.Succeeded)
                {
                    transaction.Rollback();
                    AddIdentityCreationErrors(createResult.Errors);
                    return UserAccountCreationFailure();
                }

                var roleResult = userManager.AddToRoleAsync(user.Id, model.Role).GetAwaiter().GetResult();
                if (!roleResult.Succeeded)
                {
                    transaction.Rollback();
                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError("Role", error);
                    }

                    return UserAccountCreationFailure();
                }

                var employee = new Employee
                {
                    UserId = user.Id,
                    FullName = model.FullName.Trim(),
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    DepartmentId = model.DepartmentId,
                    Gender = model.Gender,
                    DateOfBirth = model.DateOfBirth.HasValue ? model.DateOfBirth.Value.Date : (DateTime?)null,
                    Address = String.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                    AvatarPath = model.Gender == EmployeeGender.Male
                        ? "/assets/avatar/default/default_avatar_male.png"
                        : "/assets/avatar/default/default_avatar_female.png",
                    Position = model.Role,
                    HireDate = DateTime.Today,
                    CreatedByUserId = User.Identity.GetUserId(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                db.Employees.Add(employee);
                db.SaveChanges();
                transaction.Commit();
            }

            TempData["WorkspaceSuccess"] = "The user account was created.";
            if (Request.IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    message = "The user account was created."
                });
            }

            return RedirectToAction("Employees");
        }

        private ActionResult UserAccountCreationFailure()
        {
            var errors = ModelState.Where(entry => entry.Value.Errors.Count > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value.Errors
                .Select(error => String.IsNullOrWhiteSpace(error.ErrorMessage) ? "Check this value." : error.ErrorMessage)
                .ToArray());
            if (Request.IsAjaxRequest())
            {
                Response.StatusCode = 400;
                Response.TrySkipIisCustomErrors = true;
                return Json(new
                {
                    success = false,
                    errors
                });
            }

            TempData["WorkspaceError"] = String.Join(" ", errors.Values.SelectMany(messages => messages));
            return RedirectToAction("Employees");
        }

        private void AddIdentityCreationErrors(IEnumerable<string> errors)
        {
            var messages = (errors ?? Enumerable.Empty<string>()).ToList();
            var allMessages = String.Join(" ", messages).ToLowerInvariant();
            if (allMessages.Contains("user name") || allMessages.Contains("username"))
            {
                ModelState.AddModelError("Username", "This username is already in use or is not valid.");
            }
            else if (allMessages.Contains("email"))
            {
                ModelState.AddModelError("Email", "This email address is already in use or is not valid.");
            }
            else if (allMessages.Contains("password"))
            {
                ModelState.AddModelError("Password", "The password does not meet the account security requirements.");
            }
            else
            {
                ModelState.AddModelError("", "The account could not be created. Check the entered details and try again.");
            }
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult UpdateUserAccount(EditUserAccountViewModel model)
        {
            var user = db.Users.Find(model.Id);
            if (user == null)
            {
                return HttpNotFound();
            }

            if (!User.IsInRole("Admin") && (!IsUserOnlyAccount(user.Id) || model.Role != "User"))
            {
                TempData["WorkspaceError"] = "Managers can update User accounts only and cannot change their role.";
                return RedirectToAction("Employees");
            }
            if (String.Equals(user.Id, User.Identity.GetUserId(), StringComparison.Ordinal))
            {
                TempData["WorkspaceError"] = "You cannot change your own username or role here.";
                return RedirectToAction("Employees");
            }
            var adminRoleId = db.Roles.Where(role => role.Name == "Admin").Select(role => role.Id).FirstOrDefault();
            var targetIsAdmin = user.Roles.Any(role => role.RoleId == adminRoleId);
            if (!User.IsInRole("Admin") && (targetIsAdmin || model.Role == "Admin"))
            {
                TempData["WorkspaceError"] = "Only an Administrator can change an Administrator account or assign the Administrator role.";
                return RedirectToAction("Employees");
            }
            if (!db.Roles.Any(role => role.Name == model.Role))
            {
                ModelState.AddModelError("Role", "Select an existing role.");
            }

            if (!ModelState.IsValid)
            {
                TempData["WorkspaceError"] = "Check the username, email, phone number, and selected role.";
                return RedirectToAction("Employees");
            }

            var userManager = ApplicationUserManager.CreateForSeeding(db);
            var existingName = userManager.FindByNameAsync(model.Username.Trim()).GetAwaiter().GetResult();
            var existingEmail = userManager.FindByEmailAsync(model.Email.Trim()).GetAwaiter().GetResult();
            if ((existingName != null && existingName.Id != user.Id) || (existingEmail != null && existingEmail.Id != user.Id))
            {
                TempData["WorkspaceError"] = "That username or email address is already in use.";
                return RedirectToAction("Employees");
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                var oldRoles = userManager.GetRoles(user.Id);
                var adminRole = db.Roles.FirstOrDefault(role => role.Name == "Admin");
                if (oldRoles.Contains("Admin") && model.Role != "Admin" && adminRole != null
                    && db.Users.Count(candidate => candidate.Roles.Any(role => role.RoleId == adminRole.Id)
                    && (!candidate.LockoutEndDateUtc.HasValue || candidate.LockoutEndDateUtc.Value <= DateTime.UtcNow)) <= 1)
                {
                    TempData["WorkspaceError"] = "The last available Administrator cannot be changed to another role.";
                    return RedirectToAction("Employees");
                }
                var removeResult = oldRoles.Count == 0
                    ? IdentityResult.Success
                    : userManager.RemoveFromRolesAsync(user.Id, oldRoles.ToArray()).GetAwaiter().GetResult();
                var roleResult = removeResult.Succeeded ? userManager.AddToRoleAsync(user.Id, model.Role).GetAwaiter().GetResult() : removeResult;
                user.UserName = model.Username.Trim();
                var nameResult = roleResult.Succeeded ? userManager.UpdateAsync(user).GetAwaiter().GetResult() : roleResult;
                var emailResult = nameResult.Succeeded ? userManager.SetEmailAsync(user.Id, model.Email.Trim()).GetAwaiter().GetResult() : nameResult;
                var phoneResult = emailResult.Succeeded
                    ? userManager.SetPhoneNumberAsync(user.Id, String.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim())
                    .GetAwaiter()
                    .GetResult()
                    : emailResult;
                if (!phoneResult.Succeeded)
                {
                    transaction.Rollback();
                    TempData["WorkspaceError"] = String.Join(" ", phoneResult.Errors);
                    return RedirectToAction("Employees");
                }

                var employee = db.Employees.FirstOrDefault(item => item.UserId == user.Id);
                if (employee != null)
                {
                    employee.Email = user.Email;
                    employee.PhoneNumber = user.PhoneNumber;
                    employee.Position = model.Role;
                }
                userManager.UpdateSecurityStampAsync(user.Id).GetAwaiter().GetResult();
                db.SaveChanges();
                transaction.Commit();
            }
            TempData["WorkspaceSuccess"] = "The account was updated.";
            return RedirectToAction("Employees");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult ToggleUserAccount(string id)
        {
            if (String.Equals(id, User.Identity.GetUserId(), StringComparison.Ordinal))
            {
                TempData["WorkspaceError"] = "You cannot lock or deactivate your own account.";
                return RedirectToAction("Employees");
            }
            var user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            if (!User.IsInRole("Admin") && !IsUserOnlyAccount(user.Id))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var manager = ApplicationUserManager.CreateForSeeding(db);
            var roles = manager.GetRoles(id);
            var isLocked = user.LockoutEndDateUtc.HasValue && user.LockoutEndDateUtc.Value > DateTime.UtcNow;
            var adminRole = db.Roles.FirstOrDefault(role => role.Name == "Admin");
            if (!isLocked && roles.Contains("Admin") && adminRole != null
                && db.Users.Count(candidate => candidate.Roles.Any(role => role.RoleId == adminRole.Id)
                && (!candidate.LockoutEndDateUtc.HasValue || candidate.LockoutEndDateUtc.Value <= DateTime.UtcNow)) <= 1)
            {
                TempData["WorkspaceError"] = "The last available Administrator cannot be locked or deactivated.";
                return RedirectToAction("Employees");
            }
            var employee = db.Employees.FirstOrDefault(item => item.UserId == id);
            using (var transaction = db.Database.BeginTransaction())
            {
                manager.SetLockoutEnabledAsync(id, true).GetAwaiter().GetResult();
                manager.SetLockoutEndDateAsync(id, isLocked ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddYears(100)).GetAwaiter()
                    .GetResult();
                manager.UpdateSecurityStampAsync(id).GetAwaiter().GetResult();
                db.SaveChanges();
                transaction.Commit();
            }
            TempData["WorkspaceSuccess"] = isLocked ? "The account was unlocked." : "The account was locked.";
            return RedirectToAction("Employees");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult ReactivateUserAccount(string id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            if (!User.IsInRole("Admin") && !IsUserOnlyAccount(user.Id))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var employee = db.Employees.FirstOrDefault(item => item.UserId == id);
            if (employee == null)
            {
                TempData["WorkspaceError"] = "This account has no linked employee profile to reactivate.";
                return RedirectToAction("Employees");
            }
            using (var transaction = db.Database.BeginTransaction())
            {
                var manager = ApplicationUserManager.CreateForSeeding(db);
                manager.SetLockoutEnabledAsync(id, true).GetAwaiter().GetResult();
                manager.SetLockoutEndDateAsync(id, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
                manager.UpdateSecurityStampAsync(id).GetAwaiter().GetResult();
                employee.IsActive = true;
                db.SaveChanges();
                transaction.Commit();
            }
            TempData["WorkspaceSuccess"] = "The account was reactivated.";
            return RedirectToAction("Employees");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeactivateUserAccount(string id)
        {
            if (String.Equals(id, User.Identity.GetUserId(), StringComparison.Ordinal))
            {
                TempData["WorkspaceError"] = "You cannot deactivate your own account.";
                return RedirectToAction("Employees");
            }
            var user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            if (!User.IsInRole("Admin") && !IsUserOnlyAccount(user.Id))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var manager = ApplicationUserManager.CreateForSeeding(db);
            var adminRole = db.Roles.FirstOrDefault(role => role.Name == "Admin");
            var userIsAdmin = adminRole != null && user.Roles.Any(role => role.RoleId == adminRole.Id);
            if (userIsAdmin
                && db.Users.Count(candidate => candidate.Roles.Any(role => role.RoleId == adminRole.Id)
                && (!candidate.LockoutEndDateUtc.HasValue || candidate.LockoutEndDateUtc.Value <= DateTime.UtcNow)) <= 1)
            {
                TempData["WorkspaceError"] = "The last available Administrator cannot be deactivated.";
                return RedirectToAction("Employees");
            }
            using (var transaction = db.Database.BeginTransaction())
            {
                manager.SetLockoutEnabledAsync(id, true).GetAwaiter().GetResult();
                manager.SetLockoutEndDateAsync(id, DateTimeOffset.UtcNow.AddYears(100)).GetAwaiter().GetResult();
                manager.UpdateSecurityStampAsync(id).GetAwaiter().GetResult();
                var employee = db.Employees.FirstOrDefault(item => item.UserId == id);
                if (employee != null)
                {
                    employee.IsActive = false;
                }

                db.SaveChanges();
                transaction.Commit();
            }
            TempData["WorkspaceSuccess"] = "Account deactivated. Login is blocked and linked task history is preserved.";
            return RedirectToAction("Employees");
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public ActionResult DeleteUserAccount(string id)
        {
            if (String.Equals(id, User.Identity.GetUserId(), StringComparison.Ordinal))
            {
                TempData["WorkspaceError"] = "You cannot delete your own account.";
                return RedirectToAction("Employees");
            }
            var user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var adminRoleId = db.Roles.Where(role => role.Name == "Admin").Select(role => role.Id).FirstOrDefault();
            if (user.Roles.Any(role => role.RoleId == adminRoleId)
                && db.Users.Count(candidate => candidate.Roles.Any(role => role.RoleId == adminRoleId)
                && (!candidate.LockoutEndDateUtc.HasValue || candidate.LockoutEndDateUtc.Value <= DateTime.UtcNow)) <= 1)
            {
                TempData["WorkspaceError"] = "The last available Administrator account cannot be deleted.";
                return RedirectToAction("Employees");
            }
            var referenced = db.Employees.Any(employee => employee.UserId == id)
                || db.ProjectComments.Any(comment => comment.AuthorUserId == id)
                    || db.ProjectProgressReports.Any(report => report.ReporterUserId == id)
                    || db.ProjectReportAttachments.Any(file => file.UploadedByUserId == id)
                    || db.JobAttachments.Any(file => file.UploadedByUserId == id)
                    || db.JobProgressLogs.Any(log => log.UserId == id)
                    || db.TaskDeadlineRequests.Any(request => request.RequestedByUserId == id || request.DecidedByUserId == id)
                    || db.Projects.Any(project => project.CreatedByUserId == id)
                    || db.ProjectMembers.Any(member => member.AddedByUserId == id);
            if (referenced)
            {
                TempData["WorkspaceError"] = "This account is linked to employee or work records. Delete those records first or deactivate the account.";
                return RedirectToAction("Employees");
            }
            var manager = ApplicationUserManager.CreateForSeeding(db);
            var result = manager.DeleteAsync(user).GetAwaiter().GetResult();
            TempData[result.Succeeded ? "WorkspaceSuccess" : "WorkspaceError"] = result.Succeeded
                ? "Account deleted. Its activity log entries were preserved."
                : String.Join(" ", result.Errors);
            return RedirectToAction("Employees");
        }

        private bool IsUserOnlyAccount(string userId)
        {
            var assignedRoles = (from userRole in db.Set<IdentityUserRole>()
                                 join role in db.Roles on userRole.RoleId equals role.Id
                                 where userRole.UserId == userId
                                 select role.Name).ToList();
            return assignedRoles.Count == 1 && assignedRoles[0] == "User";
        }
    }
}
