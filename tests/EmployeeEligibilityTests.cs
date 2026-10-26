using System;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using EmployeeTaskManagement.Controllers;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity.EntityFramework;

internal static class EmployeeEligibilityTests
{
    private static int Main()
    {
        var employees = new[]
        {
            new Employee { EmployeeId = 1, UserId = "user", IsActive = true },
            new Employee { EmployeeId = 2, UserId = "manager", IsActive = true },
            new Employee { EmployeeId = 3, UserId = "admin", IsActive = true },
            new Employee { EmployeeId = 4, UserId = "inactive-user", IsActive = false },
            new Employee { EmployeeId = 5, UserId = "inactive-manager", IsActive = false },
            new Employee { EmployeeId = 6, UserId = "both", IsActive = true },
            new Employee { EmployeeId = 7, UserId = "unassigned", IsActive = true },
            new Employee { EmployeeId = 8, UserId = null, IsActive = true }
        }.AsQueryable();
        var roles = new[]
        {
            new IdentityRole { Id = "role-user", Name = "User" },
            new IdentityRole { Id = "role-manager", Name = "Manager" },
            new IdentityRole { Id = "role-admin", Name = "Admin" }
        }.AsQueryable();
        var memberships = new[]
        {
            new IdentityUserRole { UserId = "user", RoleId = "role-user" },
            new IdentityUserRole { UserId = "manager", RoleId = "role-manager" },
            new IdentityUserRole { UserId = "admin", RoleId = "role-admin" },
            new IdentityUserRole { UserId = "inactive-user", RoleId = "role-user" },
            new IdentityUserRole { UserId = "inactive-manager", RoleId = "role-manager" },
            new IdentityUserRole { UserId = "both", RoleId = "role-user" },
            new IdentityUserRole { UserId = "both", RoleId = "role-manager" }
        }.AsQueryable();
        var eligibility = typeof(Employee).Assembly.GetType("EmployeeTaskManagement.Models.EmployeeEligibility", true);
        var queryMethod = eligibility.GetMethod("ActiveEmployeesInRoles", BindingFlags.Static | BindingFlags.NonPublic);
        Func<string[], IQueryable<Employee>> query = names => (IQueryable<Employee>)queryMethod.Invoke(
            null, new object[] { employees, roles, memberships, names });

        var candidates = query(new[] { "User", "Manager" }).Select(employee => employee.EmployeeId).ToArray();
        Assert(candidates.Contains(1), "Active User can manage a project");
        Assert(candidates.Contains(2), "Active Manager can manage a project");
        Assert(!candidates.Contains(3), "Admin-only account is not a project manager candidate");
        Assert(!candidates.Contains(4) && !candidates.Contains(5), "Inactive User and Manager are excluded");
        Assert(candidates.Count(id => id == 6) == 1, "Multiple eligible roles do not duplicate an employee");
        Assert(!candidates.Contains(7) && !candidates.Contains(8), "Unassigned and unlinked employees are excluded");
        Assert(query(new[] { "User" }).Select(employee => employee.EmployeeId).SequenceEqual(new[] { 1, 6 }),
            "Ordinary project member eligibility still requires User role");
        Assert(!query(new[] { "MissingRole" }).Any(), "Missing role returns an empty candidate list");
        Assert(!query(new string[0]).Any(), "Empty role filter returns an empty candidate list");

        foreach (var action in new[] { "Projects", "SaveProject" })
        {
            var authorization = typeof(HomeController).GetMethod(action).GetCustomAttribute<AuthorizeAttribute>();
            Assert(authorization != null && authorization.Roles == "Admin,Manager", action + " retains its authorization");
        }
        Assert(typeof(HomeController).GetMethod("SaveProject").IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),
            "Project creation retains CSRF validation");
        Console.WriteLine("All 12 regression checks passed.");
        return 0;
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(name);
        }

        Console.WriteLine("PASS: " + name);
    }
}
