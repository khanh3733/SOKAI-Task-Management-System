using System.Data.Entity;
using EmployeeTaskManagement.Migrations;
using EmployeeTaskManagement.Models;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(EmployeeTaskManagement.Startup))]
namespace EmployeeTaskManagement
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            Database.SetInitializer(
                new MigrateDatabaseToLatestVersion<ApplicationDbContext, Configuration>());
            IdentitySeed.Initialize();
        }
    }
}
