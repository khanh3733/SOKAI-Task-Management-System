# SOKAI demo dataset

Run the seed only against the local `EmployeeTaskManagementDB` database:

```powershell
.\Seed-SokaiDemo.ps1
```

The password is read as a secure value and passed only through the seeder process environment. To generate a random local password instead:

```powershell
.\Seed-SokaiDemo.ps1 -GeneratePassword
```

The seed uses ASP.NET Identity `ApplicationUserManager`, the configured bcrypt hasher, a single database transaction, and versioned Identity claims. Re-running a completed seed adds nothing. Existing records are not updated or deleted; conflicting names or emails cause the operation to stop.

Demo logins use `sokai.admin@example.com`, `sokai.manager01@example.com` through `sokai.manager04@example.com`, and `sokai.user01@example.com` through `sokai.user20@example.com`.
