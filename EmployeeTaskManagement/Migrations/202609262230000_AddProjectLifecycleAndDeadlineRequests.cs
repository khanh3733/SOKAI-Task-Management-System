namespace EmployeeTaskManagement.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddProjectLifecycleAndDeadlineRequests : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Projects", "DueDate", c => c.DateTime());
            AddColumn("dbo.Projects", "Status", c => c.String(nullable: false, maxLength: 20, defaultValue: "Not Started"));
            AddColumn("dbo.Projects", "StatusBeforePause", c => c.String(maxLength: 20));

            CreateTable("dbo.ProjectComments", c => new
            {
                ProjectCommentId = c.Int(nullable: false, identity: true),
                ProjectId = c.Int(nullable: false),
                AuthorUserId = c.String(nullable: false, maxLength: 128),
                EmployeeId = c.Int(),
                ParentCommentId = c.Int(),
                Content = c.String(nullable: false, maxLength: 2000),
                CreatedAt = c.DateTime(nullable: false)
            }).PrimaryKey(t => t.ProjectCommentId)
              .ForeignKey("dbo.Projects", t => t.ProjectId)
              .ForeignKey("dbo.AspNetUsers", t => t.AuthorUserId)
              .ForeignKey("dbo.Employees", t => t.EmployeeId)
              .ForeignKey("dbo.ProjectComments", t => t.ParentCommentId)
              .Index(t => t.ProjectId)
              .Index(t => t.AuthorUserId)
              .Index(t => t.EmployeeId)
              .Index(t => t.ParentCommentId);

            CreateTable("dbo.TaskDeadlineRequests", c => new
            {
                TaskDeadlineRequestId = c.Int(nullable: false, identity: true),
                TaskId = c.Int(nullable: false),
                RequestedByUserId = c.String(nullable: false, maxLength: 128),
                RequestedDueDate = c.DateTime(nullable: false),
                Reason = c.String(nullable: false, maxLength: 1000),
                Status = c.String(nullable: false, maxLength: 20),
                DecidedByUserId = c.String(maxLength: 128),
                DecisionNote = c.String(maxLength: 1000),
                CreatedAt = c.DateTime(nullable: false),
                DecidedAt = c.DateTime()
            }).PrimaryKey(t => t.TaskDeadlineRequestId)
              .ForeignKey("dbo.Tasks", t => t.TaskId)
              .ForeignKey("dbo.AspNetUsers", t => t.RequestedByUserId)
              .ForeignKey("dbo.AspNetUsers", t => t.DecidedByUserId)
              .Index(t => t.TaskId)
              .Index(t => t.RequestedByUserId)
              .Index(t => t.DecidedByUserId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.TaskDeadlineRequests", "DecidedByUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.TaskDeadlineRequests", "RequestedByUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.TaskDeadlineRequests", "TaskId", "dbo.Tasks");
            DropForeignKey("dbo.ProjectComments", "ParentCommentId", "dbo.ProjectComments");
            DropForeignKey("dbo.ProjectComments", "EmployeeId", "dbo.Employees");
            DropForeignKey("dbo.ProjectComments", "AuthorUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ProjectComments", "ProjectId", "dbo.Projects");
            DropIndex("dbo.TaskDeadlineRequests", new[] { "DecidedByUserId" });
            DropIndex("dbo.TaskDeadlineRequests", new[] { "RequestedByUserId" });
            DropIndex("dbo.TaskDeadlineRequests", new[] { "TaskId" });
            DropIndex("dbo.ProjectComments", new[] { "ParentCommentId" });
            DropIndex("dbo.ProjectComments", new[] { "EmployeeId" });
            DropIndex("dbo.ProjectComments", new[] { "AuthorUserId" });
            DropIndex("dbo.ProjectComments", new[] { "ProjectId" });
            DropTable("dbo.TaskDeadlineRequests");
            DropTable("dbo.ProjectComments");
            DropColumn("dbo.Projects", "StatusBeforePause");
            DropColumn("dbo.Projects", "Status");
            DropColumn("dbo.Projects", "DueDate");
        }
    }
}
