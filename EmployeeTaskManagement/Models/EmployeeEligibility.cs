using System.Linq;
using Microsoft.AspNet.Identity.EntityFramework;

namespace EmployeeTaskManagement.Models
{
    internal static class EmployeeEligibility
    {
        public static IQueryable<Employee> ProjectManagers(ApplicationDbContext db)
        {
            return ActiveEmployeesInRoles(db.Employees, db.Roles, db.Set<IdentityUserRole>(), "User", "Manager");
        }

        public static IQueryable<Employee> ProjectMembers(ApplicationDbContext db)
        {
            return ActiveEmployeesInRoles(db.Employees, db.Roles, db.Set<IdentityUserRole>(), "User");
        }

        internal static IQueryable<Employee> ActiveEmployeesInRoles(
            IQueryable<Employee> employees,
            IQueryable<IdentityRole> roles,
            IQueryable<IdentityUserRole> userRoles,
            params string[] roleNames)
        {
            var roleIds = roles.Where(role => roleNames.Contains(role.Name)).Select(role => role.Id);
            var userIds = userRoles.Where(role => roleIds.Contains(role.RoleId)).Select(role => role.UserId);
            return employees.Where(employee => employee.IsActive && userIds.Contains(employee.UserId));
        }
    }
}
