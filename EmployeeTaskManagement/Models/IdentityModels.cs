using System.Data.Entity;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace EmployeeTaskManagement.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit https://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<TaskHistory> TaskHistories { get; set; }
        public DbSet<JobProgressLog> JobProgressLogs { get; set; }
        public DbSet<JobAttachment> JobAttachments { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        public DbSet<ProjectProgressReport> ProjectProgressReports { get; set; }
        public DbSet<ProjectReportAttachment> ProjectReportAttachments { get; set; }
        public DbSet<ProjectComment> ProjectComments { get; set; }
        public DbSet<TaskDeadlineRequest> TaskDeadlineRequests { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Task>()
                .HasRequired(t => t.AssignedEmployee)
                .WithMany()
                .HasForeignKey(t => t.AssignedEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Task>()
                .HasRequired(t => t.CreatedByEmployee)
                .WithMany()
                .HasForeignKey(t => t.CreatedByEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<JobProgressLog>()
                .HasRequired(log => log.Task).WithMany(task => task.ProgressLogs)
                .HasForeignKey(log => log.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<JobProgressLog>()
                .HasRequired(log => log.User).WithMany()
                .HasForeignKey(log => log.UserId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<JobAttachment>()
                .HasRequired(file => file.Task).WithMany(task => task.Attachments)
                .HasForeignKey(file => file.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<JobAttachment>()
                .HasRequired(file => file.UploadedBy).WithMany()
                .HasForeignKey(file => file.UploadedByUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Project>().HasRequired(project => project.ProjectManager).WithMany()
                .HasForeignKey(project => project.ProjectManagerEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Project>().HasRequired(project => project.CreatedBy).WithMany()
                .HasForeignKey(project => project.CreatedByUserId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectMember>().HasRequired(member => member.Project).WithMany(project => project.Members)
                .HasForeignKey(member => member.ProjectId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectMember>().HasRequired(member => member.Employee).WithMany()
                .HasForeignKey(member => member.EmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Task>().HasOptional(task => task.Project).WithMany(project => project.Tasks)
                .HasForeignKey(task => task.ProjectId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectProgressReport>().HasRequired(report => report.Project)
                .WithMany(project => project.ProgressReports)
                .HasForeignKey(report => report.ProjectId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectProgressReport>().HasOptional(report => report.Task).WithMany()
                .HasForeignKey(report => report.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectProgressReport>().HasRequired(report => report.Reporter).WithMany()
                .HasForeignKey(report => report.ReporterUserId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectProgressReport>().HasRequired(report => report.Recipient).WithMany()
                .HasForeignKey(report => report.RecipientEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectReportAttachment>().HasRequired(file => file.Report).WithMany(report => report.Attachments)
                .HasForeignKey(file => file.ProjectProgressReportId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectReportAttachment>().HasRequired(file => file.UploadedBy).WithMany()
                .HasForeignKey(file => file.UploadedByUserId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectComment>().HasRequired(comment => comment.Project).WithMany(project => project.Comments)
                .HasForeignKey(comment => comment.ProjectId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectComment>().HasRequired(comment => comment.Author).WithMany()
                .HasForeignKey(comment => comment.AuthorUserId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectComment>().HasOptional(comment => comment.Employee).WithMany()
                .HasForeignKey(comment => comment.EmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProjectComment>().HasOptional(comment => comment.ParentComment).WithMany(comment => comment.Replies)
                .HasForeignKey(comment => comment.ParentCommentId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskDeadlineRequest>().HasRequired(request => request.Task).WithMany()
                .HasForeignKey(request => request.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskDeadlineRequest>().HasRequired(request => request.RequestedBy).WithMany()
                .HasForeignKey(request => request.RequestedByUserId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskDeadlineRequest>().HasOptional(request => request.DecidedBy).WithMany()
                .HasForeignKey(request => request.DecidedByUserId)
                .WillCascadeOnDelete(false);
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}
