namespace EmployeeTaskManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Departments",
                c => new
                    {
                        DepartmentId = c.Int(nullable: false, identity: true),
                        DepartmentName = c.String(nullable: false, maxLength: 100),
                        Description = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.DepartmentId);
            
            CreateTable(
                "dbo.Employees",
                c => new
                    {
                        EmployeeId = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false, maxLength: 100),
                        Email = c.String(nullable: false, maxLength: 150),
                        PhoneNumber = c.String(maxLength: 30),
                        Position = c.String(maxLength: 100),
                        HireDate = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        UserId = c.String(nullable: false, maxLength: 128),
                        DepartmentId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.EmployeeId)
                .ForeignKey("dbo.Departments", t => t.DepartmentId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.DepartmentId);
            
            CreateTable(
                "dbo.AspNetUsers",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "dbo.AspNetUserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserRoles",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        RoleId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "dbo.AspNetRoles",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Name = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
            CreateTable(
                "dbo.TaskComments",
                c => new
                    {
                        TaskCommentId = c.Int(nullable: false, identity: true),
                        TaskId = c.Int(nullable: false),
                        EmployeeId = c.Int(nullable: false),
                        Comment = c.String(nullable: false, maxLength: 2000),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TaskCommentId)
                .ForeignKey("dbo.Employees", t => t.EmployeeId, cascadeDelete: true)
                .ForeignKey("dbo.Tasks", t => t.TaskId, cascadeDelete: true)
                .Index(t => t.TaskId)
                .Index(t => t.EmployeeId);
            
            CreateTable(
                "dbo.Tasks",
                c => new
                    {
                        TaskId = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 2000),
                        AssignedEmployeeId = c.Int(nullable: false),
                        CreatedByEmployeeId = c.Int(nullable: false),
                        Status = c.String(nullable: false, maxLength: 30),
                        Priority = c.String(nullable: false, maxLength: 20),
                        Progress = c.Int(nullable: false),
                        StartDate = c.DateTime(),
                        DueDate = c.DateTime(),
                        CompletedDate = c.DateTime(),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TaskId)
                .ForeignKey("dbo.Employees", t => t.AssignedEmployeeId)
                .ForeignKey("dbo.Employees", t => t.CreatedByEmployeeId)
                .Index(t => t.AssignedEmployeeId)
                .Index(t => t.CreatedByEmployeeId);
            
            CreateTable(
                "dbo.TaskHistories",
                c => new
                    {
                        TaskHistoryId = c.Int(nullable: false, identity: true),
                        TaskId = c.Int(nullable: false),
                        EmployeeId = c.Int(nullable: false),
                        Action = c.String(nullable: false, maxLength: 50),
                        Details = c.String(maxLength: 1000),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TaskHistoryId)
                .ForeignKey("dbo.Employees", t => t.EmployeeId, cascadeDelete: true)
                .ForeignKey("dbo.Tasks", t => t.TaskId, cascadeDelete: true)
                .Index(t => t.TaskId)
                .Index(t => t.EmployeeId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TaskHistories", "TaskId", "dbo.Tasks");
            DropForeignKey("dbo.TaskHistories", "EmployeeId", "dbo.Employees");
            DropForeignKey("dbo.Tasks", "CreatedByEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.TaskComments", "TaskId", "dbo.Tasks");
            DropForeignKey("dbo.Tasks", "AssignedEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.TaskComments", "EmployeeId", "dbo.Employees");
            DropForeignKey("dbo.AspNetUserRoles", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.Employees", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserRoles", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserLogins", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserClaims", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.Employees", "DepartmentId", "dbo.Departments");
            DropIndex("dbo.TaskHistories", new[] { "EmployeeId" });
            DropIndex("dbo.TaskHistories", new[] { "TaskId" });
            DropIndex("dbo.Tasks", new[] { "CreatedByEmployeeId" });
            DropIndex("dbo.Tasks", new[] { "AssignedEmployeeId" });
            DropIndex("dbo.TaskComments", new[] { "EmployeeId" });
            DropIndex("dbo.TaskComments", new[] { "TaskId" });
            DropIndex("dbo.AspNetRoles", "RoleNameIndex");
            DropIndex("dbo.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "UserId" });
            DropIndex("dbo.AspNetUserLogins", new[] { "UserId" });
            DropIndex("dbo.AspNetUserClaims", new[] { "UserId" });
            DropIndex("dbo.AspNetUsers", "UserNameIndex");
            DropIndex("dbo.Employees", new[] { "DepartmentId" });
            DropIndex("dbo.Employees", new[] { "UserId" });
            DropTable("dbo.TaskHistories");
            DropTable("dbo.Tasks");
            DropTable("dbo.TaskComments");
            DropTable("dbo.AspNetRoles");
            DropTable("dbo.AspNetUserRoles");
            DropTable("dbo.AspNetUserLogins");
            DropTable("dbo.AspNetUserClaims");
            DropTable("dbo.AspNetUsers");
            DropTable("dbo.Employees");
            DropTable("dbo.Departments");
        }
    }
}
