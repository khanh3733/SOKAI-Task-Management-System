
namespace EmployeeTaskManagement.Models
{
    internal static class ActivityLogSchema
    {
        private static readonly object Sync = new object();
        private static bool initialized;

        public static void EnsureCreated(ApplicationDbContext context)
        {
            if (initialized)
            {
                return;
            }

            lock (Sync)
            {
                if (initialized)
                {
                    return;
                }

                context.Database.ExecuteSqlCommand(@"
IF OBJECT_ID(N'dbo.ActivityLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ActivityLogs (
        ActivityLogId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ActivityLogs PRIMARY KEY,
        UserId nvarchar(128) NULL,
        UserName nvarchar(256) NOT NULL,
        Controller nvarchar(100) NOT NULL,
        Action nvarchar(100) NOT NULL,
        HttpMethod nvarchar(10) NOT NULL,
        Result nvarchar(20) NOT NULL,
        IpAddress nvarchar(45) NULL,
        CreatedAt datetime2 NOT NULL
    );
    CREATE INDEX IX_ActivityLogs_CreatedAt ON dbo.ActivityLogs(CreatedAt);
END");
                initialized = true;
            }
        }
    }
}
