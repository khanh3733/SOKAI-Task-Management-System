namespace EmployeeTaskManagement.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddProjectsAndProjectReports : DbMigration
    {
        public override void Up()
        {
            CreateTable("dbo.Projects", c => new
            {
                ProjectId = c.Int(nullable: false, identity: true),
                Name = c.String(nullable: false, maxLength: 150),
                Description = c.String(maxLength: 2000),
                ProjectManagerEmployeeId = c.Int(nullable: false),
                CreatedByUserId = c.String(nullable: false, maxLength: 128),
                CreatedAt = c.DateTime(nullable: false)
            }).PrimaryKey(t => t.ProjectId)
              .ForeignKey("dbo.Employees", t => t.ProjectManagerEmployeeId)
              .ForeignKey("dbo.AspNetUsers", t => t.CreatedByUserId)
              .Index(t => t.ProjectManagerEmployeeId)
              .Index(t => t.CreatedByUserId);

            CreateTable("dbo.ProjectMembers", c => new
            {
                ProjectMemberId = c.Int(nullable: false, identity: true),
                ProjectId = c.Int(nullable: false),
                EmployeeId = c.Int(nullable: false),
                AddedByUserId = c.String(nullable: false, maxLength: 128),
                AddedAt = c.DateTime(nullable: false)
            }).PrimaryKey(t => t.ProjectMemberId)
              .ForeignKey("dbo.Projects", t => t.ProjectId)
              .ForeignKey("dbo.Employees", t => t.EmployeeId)
              .Index(t => t.ProjectId)
              .Index(t => t.EmployeeId)
              .Index(t => new { t.ProjectId, t.EmployeeId }, unique: true, name: "IX_ProjectMembers_Project_Employee");

            AddColumn("dbo.Tasks", "ProjectId", c => c.Int());
            CreateIndex("dbo.Tasks", "ProjectId");
            AddForeignKey("dbo.Tasks", "ProjectId", "dbo.Projects", "ProjectId");

            CreateTable("dbo.ProjectProgressReports", c => new
            {
                ProjectProgressReportId = c.Int(nullable: false, identity: true),
                ProjectId = c.Int(nullable: false),
                TaskId = c.Int(),
                ReporterUserId = c.String(nullable: false, maxLength: 128),
                RecipientEmployeeId = c.Int(nullable: false),
                TaskCompleted = c.Boolean(),
                ReportType = c.String(nullable: false, maxLength: 20),
                Note = c.String(maxLength: 2000),
                CreatedAt = c.DateTime(nullable: false)
            }).PrimaryKey(t => t.ProjectProgressReportId)
              .ForeignKey("dbo.Projects", t => t.ProjectId)
              .ForeignKey("dbo.Tasks", t => t.TaskId)
              .ForeignKey("dbo.AspNetUsers", t => t.ReporterUserId)
              .ForeignKey("dbo.Employees", t => t.RecipientEmployeeId)
              .Index(t => t.ProjectId)
              .Index(t => t.TaskId)
              .Index(t => t.ReporterUserId)
              .Index(t => t.RecipientEmployeeId);

            CreateTable("dbo.ProjectReportAttachments", c => new
            {
                ProjectReportAttachmentId = c.Int(nullable: false, identity: true),
                ProjectProgressReportId = c.Int(nullable: false),
                FileName = c.String(nullable: false, maxLength: 255),
                StoredFileName = c.String(nullable: false, maxLength: 100),
                MimeType = c.String(nullable: false, maxLength: 150),
                SizeBytes = c.Long(nullable: false),
                UploadedByUserId = c.String(nullable: false, maxLength: 128),
                UploadedAt = c.DateTime(nullable: false)
            }).PrimaryKey(t => t.ProjectReportAttachmentId)
              .ForeignKey("dbo.ProjectProgressReports", t => t.ProjectProgressReportId)
              .ForeignKey("dbo.AspNetUsers", t => t.UploadedByUserId)
              .Index(t => t.ProjectProgressReportId)
              .Index(t => t.UploadedByUserId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.ProjectReportAttachments", "UploadedByUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ProjectReportAttachments", "ProjectProgressReportId", "dbo.ProjectProgressReports");
            DropForeignKey("dbo.ProjectProgressReports", "RecipientEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.ProjectProgressReports", "ReporterUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ProjectProgressReports", "TaskId", "dbo.Tasks");
            DropForeignKey("dbo.ProjectProgressReports", "ProjectId", "dbo.Projects");
            DropForeignKey("dbo.Tasks", "ProjectId", "dbo.Projects");
            DropForeignKey("dbo.ProjectMembers", "EmployeeId", "dbo.Employees");
            DropForeignKey("dbo.ProjectMembers", "ProjectId", "dbo.Projects");
            DropForeignKey("dbo.Projects", "CreatedByUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.Projects", "ProjectManagerEmployeeId", "dbo.Employees");
            DropIndex("dbo.ProjectReportAttachments", new[] { "UploadedByUserId" });
            DropIndex("dbo.ProjectReportAttachments", new[] { "ProjectProgressReportId" });
            DropIndex("dbo.ProjectProgressReports", new[] { "RecipientEmployeeId" });
            DropIndex("dbo.ProjectProgressReports", new[] { "ReporterUserId" });
            DropIndex("dbo.ProjectProgressReports", new[] { "TaskId" });
            DropIndex("dbo.ProjectProgressReports", new[] { "ProjectId" });
            DropIndex("dbo.Tasks", new[] { "ProjectId" });
            DropIndex("dbo.ProjectMembers", "IX_ProjectMembers_Project_Employee");
            DropIndex("dbo.ProjectMembers", new[] { "EmployeeId" });
            DropIndex("dbo.ProjectMembers", new[] { "ProjectId" });
            DropIndex("dbo.Projects", new[] { "CreatedByUserId" });
            DropIndex("dbo.Projects", new[] { "ProjectManagerEmployeeId" });
            DropTable("dbo.ProjectReportAttachments");
            DropTable("dbo.ProjectProgressReports");
            DropColumn("dbo.Tasks", "ProjectId");
            DropTable("dbo.ProjectMembers");
            DropTable("dbo.Projects");
        }
    }
}
