using System.Web.Mvc;
using EmployeeTaskManagement.Filters;

namespace EmployeeTaskManagement
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new ActivityLogFilter());
        }
    }
}
