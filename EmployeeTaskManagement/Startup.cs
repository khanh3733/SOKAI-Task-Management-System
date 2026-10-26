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
            IdentitySeed.Initialize();
        }
    }
}
