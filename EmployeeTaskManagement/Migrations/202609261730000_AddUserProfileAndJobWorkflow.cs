namespace EmployeeTaskManagement.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddUserProfileAndJobWorkflow : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Employees", "DepartmentId", c => c.Int());
            AddColumn("dbo.Employees", "Gender", c => c.Int());
            AddColumn("dbo.Employees", "DateOfBirth", c => c.DateTime());
            AddColumn("dbo.Employees", "Address", c => c.String(maxLength: 500));
            AddColumn("dbo.Employees", "AvatarPath", c => c.String(maxLength: 260));
            AddColumn("dbo.Employees", "CreatedByUserId", c => c.String(maxLength: 128));
            AddColumn("dbo.Employees", "CreatedAt", c => c.DateTime(nullable: false, defaultValueSql: "GETUTCDATE()"));

            CreateTable("dbo.JobProgressLogs", c => new
            {
                JobProgressLogId = c.Int(nullable: false, identity: true),
                TaskId = c.Int(nullable: false),
                UserId = c.String(nullable: false, maxLength: 128),
                Status = c.String(nullable: false, maxLength: 30),
                Percent = c.Int(nullable: false),
                Note = c.String(maxLength: 2000),
                CreatedAt = c.DateTime(nullable: false)
            }).PrimaryKey(t => t.JobProgressLogId)
              .ForeignKey("dbo.Tasks", t => t.TaskId)
              .ForeignKey("dbo.AspNetUsers", t => t.UserId)
              .Index(t => t.TaskId)
              .Index(t => t.UserId);

            CreateTable("dbo.JobAttachments", c => new
            {
                JobAttachmentId = c.Int(nullable: false, identity: true),
                TaskId = c.Int(nullable: false),
                FileName = c.String(nullable: false, maxLength: 255),
                StoredFileName = c.String(nullable: false, maxLength: 100),
                MimeType = c.String(nullable: false, maxLength: 150),
                SizeBytes = c.Long(nullable: false),
                UploadedByUserId = c.String(nullable: false, maxLength: 128),
                UploadedAt = c.DateTime(nullable: false)
            }).PrimaryKey(t => t.JobAttachmentId)
              .ForeignKey("dbo.Tasks", t => t.TaskId)
              .ForeignKey("dbo.AspNetUsers", t => t.UploadedByUserId)
              .Index(t => t.TaskId)
              .Index(t => t.UploadedByUserId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.JobAttachments", "UploadedByUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.JobAttachments", "TaskId", "dbo.Tasks");
            DropForeignKey("dbo.JobProgressLogs", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.JobProgressLogs", "TaskId", "dbo.Tasks");
            DropIndex("dbo.JobAttachments", new[] { "UploadedByUserId" });
            DropIndex("dbo.JobAttachments", new[] { "TaskId" });
            DropIndex("dbo.JobProgressLogs", new[] { "UserId" });
            DropIndex("dbo.JobProgressLogs", new[] { "TaskId" });
            DropTable("dbo.JobAttachments");
            DropTable("dbo.JobProgressLogs");
            DropColumn("dbo.Employees", "CreatedAt");
            DropColumn("dbo.Employees", "CreatedByUserId");
            DropColumn("dbo.Employees", "AvatarPath");
            DropColumn("dbo.Employees", "Address");
            DropColumn("dbo.Employees", "DateOfBirth");
            DropColumn("dbo.Employees", "Gender");
            AlterColumn("dbo.Employees", "DepartmentId", c => c.Int(nullable: false));
        }
    }
}
