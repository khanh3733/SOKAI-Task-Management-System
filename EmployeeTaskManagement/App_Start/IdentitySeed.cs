using System;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace EmployeeTaskManagement
{
    /// <summary>
    /// Creates the initial roles and any explicitly configured bootstrap accounts.
    /// Existing users are never modified.
    /// </summary>
    internal static class IdentitySeed
    {
        private const string SeedClaimType = "urn:employee-task-management:initial-account";

        public static void Initialize()
        {
            using (var context = new ApplicationDbContext())
            using (var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context)))
            using (var userManager = ApplicationUserManager.CreateForSeeding(context))
            {
                EnsureRole(roleManager, "Admin");
                EnsureRole(roleManager, "Manager");
                EnsureRole(roleManager, "User");

                EnsureConfiguredAccount(userManager, context, "InitialAdminEmail", "InitialAdminPassword", "Admin", "admin");
                EnsureConfiguredAccount(userManager, context, "InitialUserEmail", "InitialUserPassword", "User", "user");
            }
        }

        private static void EnsureConfiguredAccount(
            ApplicationUserManager userManager,
            ApplicationDbContext context,
            string emailSetting,
            string passwordSetting,
            string roleName,
            string seedKey)
        {
            var email = ConfigurationManager.AppSettings[emailSetting];
            var password = ConfigurationManager.AppSettings[passwordSetting];

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new ConfigurationErrorsException(
                    "Both '" + emailSetting + "' and '" + passwordSetting + "' must be configured together.");
            }

            EnsureAccount(userManager, context, email.Trim(), password, roleName, seedKey);
        }

        private static void EnsureRole(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
            {
                return;
            }

            var result = roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
            if (!result.Succeeded && !roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
            {
                throw new InvalidOperationException(
                    "Could not create the initial '" + roleName + "' role: " + FormatErrors(result));
            }
        }

        private static void EnsureAccount(
            ApplicationUserManager userManager,
            ApplicationDbContext context,
            string email,
            string password,
            string roleName,
            string seedKey)
        {
            // Do not update an existing user's password, email, or role.
            if (FindExistingAccount(userManager, context, email, seedKey) != null)
            {
                return;
            }

            IdentityResult createResult;
            IdentityResult roleResult = null;
            IdentityResult claimResult = null;

            // Keep account creation and role assignment atomic. A failed role assignment must not
            // leave behind an account that a later startup would correctly treat as pre-existing.
            using (var transaction = context.Database.BeginTransaction())
            {
                if (FindExistingAccount(userManager, context, email, seedKey) != null)
                {
                    transaction.Commit();
                    return;
                }

                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email
                };

                createResult = userManager.CreateAsync(user, password).GetAwaiter().GetResult();
                if (createResult.Succeeded)
                {
                    roleResult = userManager.AddToRoleAsync(user.Id, roleName).GetAwaiter().GetResult();
                    if (roleResult.Succeeded)
                    {
                        claimResult = userManager.AddClaimAsync(
                            user.Id,
                            new Claim(SeedClaimType, seedKey))
                                .GetAwaiter()
                                .GetResult();
                    }
                }

                if (createResult.Succeeded && roleResult.Succeeded && claimResult.Succeeded)
                {
                    transaction.Commit();
                    return;
                }

                transaction.Rollback();
            }

            // If another application instance created the account concurrently, leave its data
            // untouched. Otherwise surface Identity's validation/storage errors during startup.
            if (FindExistingAccount(userManager, context, email, seedKey) != null)
            {
                return;
            }

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create the initial account '" + email + "': " + FormatErrors(createResult));
            }

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not assign the initial account '" + email + "' to role '" + roleName + "': " + FormatErrors(roleResult));
            }

            throw new InvalidOperationException(
                "Could not mark the initial account '" + email + "' as seeded: " + FormatErrors(claimResult));
        }

        private static ApplicationUser FindExistingAccount(
            ApplicationUserManager userManager,
            ApplicationDbContext context,
            string email,
            string seedKey)
        {
            // The private marker survives email/password changes and avoids recreating a renamed seed account.
            var seededUser = context.Users.FirstOrDefault(user =>
                user.Claims.Any(claim => claim.ClaimType == SeedClaimType && claim.ClaimValue == seedKey));
            if (seededUser != null)
            {
                return seededUser;
            }

            var existingUser = userManager.FindByEmailAsync(email).GetAwaiter().GetResult();
            return existingUser ?? userManager.FindByNameAsync(email).GetAwaiter().GetResult();
        }

        private static string FormatErrors(IdentityResult result)
        {
            return string.Join("; ", result.Errors ?? Enumerable.Empty<string>());
        }
    }
}
