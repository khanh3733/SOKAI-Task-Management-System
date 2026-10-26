using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Web.Mvc;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity;

namespace EmployeeTaskManagement.Filters
{
    public class ActivityLogFilter : ActionFilterAttribute
    {
        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            var identity = filterContext.HttpContext.User == null ? null : filterContext.HttpContext.User.Identity;
            if (identity == null || !identity.IsAuthenticated)
            {
                return;
            }

            try
            {
                Record(filterContext.HttpContext.User.Identity.GetUserId(), identity.Name,
                    Convert.ToString(filterContext.RouteData.Values["controller"]),
                    Convert.ToString(filterContext.RouteData.Values["action"]),
                    filterContext.HttpContext.Request.HttpMethod,
                    filterContext.Exception != null ? "Error" : filterContext.HttpContext.Response.StatusCode.ToString(),
                    filterContext.HttpContext.Request.UserHostAddress);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Activity log write failed: {0}", exception);
            }
        }

        public static void Record(string userId, string userName, string controller, string action, string method, string result, string ipAddress)
        {
            try
            {
                using (var db = new ApplicationDbContext())
                {
                    ActivityLogSchema.EnsureCreated(db);
                    db.Database.ExecuteSqlCommand(
                        "INSERT INTO dbo.ActivityLogs (UserId, UserName, Controller, Action, HttpMethod, Result, IpAddress, CreatedAt) VALUES (@UserId, @UserName, @Controller, @Action, @HttpMethod, @Result, @IpAddress, @CreatedAt)",
                        new SqlParameter("@UserId", (object)userId ?? DBNull.Value),
                        new SqlParameter("@UserName", String.IsNullOrWhiteSpace(userName) ? "Unknown" : userName),
                        new SqlParameter("@Controller", String.IsNullOrWhiteSpace(controller) ? "Unknown" : controller),
                        new SqlParameter("@Action", String.IsNullOrWhiteSpace(action) ? "Unknown" : action),
                        new SqlParameter("@HttpMethod", String.IsNullOrWhiteSpace(method) ? "GET" : method),
                        new SqlParameter("@Result", String.IsNullOrWhiteSpace(result) ? "Unknown" : result),
                        new SqlParameter("@IpAddress", (object)ipAddress ?? DBNull.Value),
                        new SqlParameter("@CreatedAt", DateTime.UtcNow));
                }
            }
            catch (Exception exception) { Trace.TraceError("Activity log write failed: {0}", exception); }
        }
    }
}
