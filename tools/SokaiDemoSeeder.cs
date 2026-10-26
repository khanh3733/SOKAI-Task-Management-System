using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using EmployeeTaskManagement;
using EmployeeTaskManagement.Models;
using Microsoft.AspNet.Identity;

internal static class SokaiDemoSeeder
{
    private const string MarkerType = "urn:employee-task-management:demo-dataset";
    private const string MarkerPrefix = "sokai-demo-v1:";
    private const int UserCount = 25;

    private static readonly string[] DepartmentNames =
    {
        "システム開発部", "品質保証部", "プロジェクト推進部", "IT基盤部", "管理部"
    };

    private static readonly string[] ProjectNames =
    {
        "社内人事管理システム", "勤怠管理システム", "在庫管理システム", "顧客管理システム", "ECサイト開発",
        "社内ポータル開発", "予約管理システム", "売上分析システム", "問い合わせ管理システム", "業務改善システム"
    };

    private static readonly string[] TaskSubjects =
    {
        "要件ヒアリング結果の整理", "画面遷移図の更新", "ログイン画面の入力チェック実装",
        "一覧画面の検索条件追加", "詳細画面の権限制御実装", "登録処理のトランザクション対応",
        "CSV出力機能の実装", "通知メール文面の調整", "SQLクエリの性能改善",
        "単体テストケースの作成", "結合テストで検出した不具合の修正", "操作マニュアルのレビュー",
        "本番移行手順の確認"
    };

    private static int Main(string[] args)
    {
        if (args.Length != 1 || String.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Usage: SokaiDemoSeeder.exe <web-project-root>");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable("SOKAI_DEMO_PASSWORD");
        if (String.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("SOKAI_DEMO_PASSWORD is required in the current process.");
            return 2;
        }

        try
        {
            Seed(Path.GetFullPath(args[0]), password);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Seed(string webRoot, string password)
    {
        using (var db = new ApplicationDbContext())
        {
            var markedUsers = db.Users.Count(user => user.Claims.Any(claim =>
                claim.ClaimType == MarkerType && claim.ClaimValue.StartsWith(MarkerPrefix)));
            if (markedUsers == UserCount)
            {
                Console.WriteLine("SOKAI demo dataset already exists; no rows were added.");
                return;
            }
            if (markedUsers != 0)
            {
                throw new InvalidOperationException("Partial SOKAI dataset detected (" + markedUsers + "/" + UserCount + "). No data was changed.");
            }

            var people = BuildPeople();
            EnsureNoConflicts(db, people);
            var createdFiles = new List<string>();
            using (var manager = ApplicationUserManager.CreateForSeeding(db))
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var users = CreateUsers(manager, people, password);
                    var departments = CreateDepartments(db);
                    var employees = CreateEmployees(db, people, users, departments);
                    db.SaveChanges();

                    var memberLookup = new Dictionary<Project, IList<Employee>>();
                    var projects = CreateProjects(db, employees, users[0], memberLookup);
                    var tasks = CreateTasks(db, projects, memberLookup);
                    CreateProjectComments(db, projects, memberLookup);
                    CreateTaskTimeline(db, tasks);
                    CreateDeadlineRequests(db, tasks, users[0]);
                    var reports = CreateReports(db, projects, tasks, memberLookup);
                    CreateAttachments(db, webRoot, tasks, reports, createdFiles);
                    CreateActivityLogsIfNeeded(db, users);

                    db.ChangeTracker.DetectChanges();
                    ValidateCounts(db, departments, employees, projects, tasks, reports);
                    db.SaveChanges();
                    transaction.Commit();
                    Console.WriteLine("SOKAI demo dataset created successfully.");
                }
                catch
                {
                    transaction.Rollback();
                    foreach (var file in createdFiles.Where(File.Exists)) File.Delete(file);
                    throw;
                }
            }
        }
    }

    private static void EnsureNoConflicts(ApplicationDbContext db, IList<PersonSeed> people)
    {
        var emails = people.Select(person => person.Email).ToList();
        if (db.Users.Any(user => emails.Contains(user.Email) || emails.Contains(user.UserName)))
            throw new InvalidOperationException("A SOKAI demo email already exists without the dataset marker.");
        if (db.Departments.Any(department => DepartmentNames.Contains(department.DepartmentName)))
            throw new InvalidOperationException("A SOKAI department name already exists without the dataset marker.");
        if (db.Projects.Any(project => ProjectNames.Contains(project.Name)))
            throw new InvalidOperationException("A SOKAI project name already exists without the dataset marker.");
    }

    private static IList<ApplicationUser> CreateUsers(ApplicationUserManager manager, IList<PersonSeed> people, string password)
    {
        var users = new List<ApplicationUser>();
        for (var index = 0; index < people.Count; index++)
        {
            var person = people[index];
            var user = new ApplicationUser
            {
                UserName = person.Email,
                Email = person.Email,
                EmailConfirmed = true,
                PhoneNumber = person.Phone,
                PhoneNumberConfirmed = true,
                LockoutEnabled = true
            };
            Ensure(manager.Create(user, password), "create account " + person.Email);
            Ensure(manager.AddToRole(user.Id, person.Role), "assign role " + person.Role);
            Ensure(manager.AddClaim(user.Id, new Claim(MarkerType, MarkerPrefix + index.ToString("D2", CultureInfo.InvariantCulture))), "add dataset marker");
            users.Add(user);
        }
        return users;
    }

    private static IList<Department> CreateDepartments(ApplicationDbContext db)
    {
        var descriptions = new[]
        {
            "業務システムとWebサービスの設計・開発を担当します。（SOKAIデモ）",
            "品質計画、テスト設計、受入確認を横断的に支援します。（SOKAIデモ）",
            "案件計画、進捗管理、お客様との調整を担当します。（SOKAIデモ）",
            "クラウド、ネットワーク、セキュリティ基盤を運用します。（SOKAIデモ）",
            "人事、総務、経理など社内業務を支援します。（SOKAIデモ）"
        };
        var result = DepartmentNames.Select((name, index) => new Department { DepartmentName = name, Description = descriptions[index] }).ToList();
        db.Departments.AddRange(result);
        return result;
    }

    private static IList<Employee> CreateEmployees(ApplicationDbContext db, IList<PersonSeed> people,
        IList<ApplicationUser> users, IList<Department> departments)
    {
        var today = DateTime.UtcNow.Date;
        var result = new List<Employee>();
        for (var index = 0; index < people.Count; index++)
        {
            var person = people[index];
            var employee = new Employee
            {
                User = users[index], UserId = users[index].Id, FullName = person.Name, Email = person.Email,
                PhoneNumber = person.Phone, Position = person.Position,
                HireDate = today.AddYears(-(2 + index % 9)).AddDays(index * 3), IsActive = true,
                Gender = index % 2 == 0 ? EmployeeGender.Female : EmployeeGender.Male,
                DateOfBirth = today.AddYears(-(27 + index % 18)).AddDays(-(index * 11)),
                Address = "東京都千代田区サンプル" + (index + 1) + "丁目",
                Department = departments[index % departments.Count], CreatedByUserId = users[0].Id,
                CreatedAt = today.AddDays(-420 + index)
            };
            db.Employees.Add(employee);
            result.Add(employee);
        }
        return result;
    }

    private static IList<Project> CreateProjects(ApplicationDbContext db, IList<Employee> employees,
        ApplicationUser administrator, IDictionary<Project, IList<Employee>> memberLookup)
    {
        var now = DateTime.UtcNow;
        var descriptions = new[]
        {
            "社員情報、所属、権限を一元管理し、人事異動時の更新作業を効率化します。",
            "打刻、休暇申請、月次締め、勤怠データ出力までを支援します。",
            "入出庫、棚卸、発注点を可視化し、欠品と過剰在庫を抑えます。",
            "商談履歴、担当者、フォロー予定を共有し、営業活動を標準化します。",
            "商品検索、カート、注文、決済、出荷連携を備えた法人向けECサイトです。",
            "社内通知、規程検索、申請リンクを集約する従業員向けポータルです。",
            "会議室と来訪予約の空き状況確認、承認、通知を行います。",
            "部門別・商品別の売上を可視化し、月次会議の意思決定を支援します。",
            "問い合わせの受付、担当割当、回答期限、ナレッジ化を管理します。",
            "紙と表計算で行っている申請・承認業務をワークフロー化します。"
        };
        var statuses = new[] { "In Progress", "In Progress", "Paused", "In Progress", "In Progress", "Ready", "In Progress", "In Progress", "Paused", "Not Started" };
        var dueDays = new[] { 60, 45, 30, 90, 120, 100, 70, 40, 25, 150 };
        var userEmployees = employees.Skip(5).ToList();
        var result = new List<Project>();
        for (var index = 0; index < ProjectNames.Length; index++)
        {
            var manager = employees[1 + index % 4];
            var project = new Project
            {
                Name = ProjectNames[index], Description = descriptions[index] + "（SOKAIデモ）",
                DueDate = now.Date.AddDays(dueDays[index]), Status = statuses[index],
                StatusBeforePause = statuses[index] == "Paused" ? "In Progress" : null,
                ProjectManager = manager, CreatedBy = administrator, CreatedByUserId = administrator.Id,
                CreatedAt = now.AddDays(-(150 - index * 11))
            };
            db.Projects.Add(project);
            result.Add(project);
            var members = new List<Employee> { manager };
            var memberCount = statuses[index] == "Not Started" ? 0 : statuses[index] == "Ready" ? 3 : 5;
            for (var memberIndex = 0; memberIndex < memberCount; memberIndex++)
                members.Add(userEmployees[(index * 3 + memberIndex) % userEmployees.Count]);
            foreach (var member in members.Distinct())
            {
                project.Members.Add(new ProjectMember
                {
                    Employee = member, AddedByUserId = member == manager ? administrator.Id : manager.UserId,
                    AddedAt = project.CreatedAt.AddDays(member == manager ? 0 : 3 + members.IndexOf(member))
                });
            }
            memberLookup.Add(project, members.Distinct().ToList());
        }
        return result;
    }

    private static IList<EmployeeTaskManagement.Models.Task> CreateTasks(ApplicationDbContext db, IList<Project> projects,
        IDictionary<Project, IList<Employee>> memberLookup)
    {
        var now = DateTime.UtcNow;
        var activeProjects = projects.Where(project => project.Status == "In Progress" || project.Status == "Paused").ToList();
        var result = new List<EmployeeTaskManagement.Models.Task>();
        for (var index = 0; index < 100; index++)
        {
            var project = activeProjects[index % activeProjects.Count];
            var assignees = memberLookup[project].Where(employee => employee != project.ProjectManager).ToList();
            var status = TaskStatus(index);
            var created = now.AddDays(-(110 - index % 65)).AddHours(index % 8);
            var due = status == "Completed" ? now.Date.AddDays(-5 - index % 20)
                : index % 7 == 0 ? now.Date.AddDays(-(1 + index % 12)) : now.Date.AddDays(5 + index % 35);
            if (project.DueDate.HasValue && due > project.DueDate.Value.Date) due = project.DueDate.Value.Date;
            var completed = status == "Completed" ? (DateTime?)due.AddDays(-1) : null;
            var task = new EmployeeTaskManagement.Models.Task
            {
                Project = project, Title = project.Name.Replace("システム", "") + "：" + TaskSubjects[index % TaskSubjects.Length],
                Description = "設計資料とコーディング規約に沿って対応し、完了時に確認結果を報告してください。",
                AssignedEmployee = assignees[index % assignees.Count], CreatedByEmployee = project.ProjectManager,
                Status = status, Priority = index % 10 < 2 ? "High" : index % 10 < 5 ? "Low" : "Medium",
                Progress = TaskProgress(index, status), StartDate = created.Date.AddDays(1), DueDate = due,
                CompletedDate = completed, CreatedAt = created,
                UpdatedAt = completed ?? (status == "Pending" || status == "Not Started" ? created : created.AddDays(2 + index % 8))
            };
            db.Tasks.Add(task);
            result.Add(task);
        }
        return result;
    }

    private static void CreateProjectComments(ApplicationDbContext db, IList<Project> projects,
        IDictionary<Project, IList<Employee>> memberLookup)
    {
        var texts = new[]
        {
            "今週の進捗を確認しました。大きな課題はありません。",
            "次回定例までに未確定事項を課題一覧へ追記してください。",
            "レビュー日程を更新しましたので、担当者は確認をお願いします。"
        };
        foreach (var project in projects)
        {
            var participants = memberLookup[project];
            ProjectComment root = null;
            for (var index = 0; index < 3; index++)
            {
                var employee = participants[index % participants.Count];
                var comment = new ProjectComment
                {
                    Project = project, Author = employee.User, AuthorUserId = employee.UserId, Employee = employee,
                    Content = texts[index], CreatedAt = project.CreatedAt.AddDays(10 + index * 7)
                };
                db.ProjectComments.Add(comment);
                if (root == null) root = comment;
            }
            var replier = participants[Math.Min(1, participants.Count - 1)];
            db.ProjectComments.Add(new ProjectComment
            {
                Project = project, Author = replier.User, AuthorUserId = replier.UserId, Employee = replier,
                ParentComment = root, Content = "承知しました。担当分を確認し、次回定例で共有します。",
                CreatedAt = root.CreatedAt.AddHours(5)
            });
        }
    }

    private static void CreateTaskTimeline(ApplicationDbContext db, IList<EmployeeTaskManagement.Models.Task> tasks)
    {
        for (var index = 0; index < tasks.Count; index++)
        {
            var task = tasks[index];
            task.Histories.Add(new TaskHistory
            {
                Employee = task.CreatedByEmployee, Action = "Created", Details = "担当者と期限を設定してタスクを作成しました。", CreatedAt = task.CreatedAt
            });
            if (task.Status != "Pending" && task.Status != "Not Started")
            {
                task.Histories.Add(new TaskHistory
                {
                    Employee = task.AssignedEmployee, Action = task.Status == "Confirmed" ? "Confirmed" : "Progress Report",
                    Details = task.Status == "Confirmed" ? "担当者が作業内容を確認しました。" : "進捗を" + task.Progress + "%に更新しました。",
                    CreatedAt = task.CreatedAt.AddDays(1)
                });
                db.JobProgressLogs.Add(new JobProgressLog
                {
                    Task = task, User = task.AssignedEmployee.User, Status = LogStatus(task.Status), Percent = task.Progress,
                    Note = ProgressNote(task.Status), CreatedAt = task.CreatedAt.AddDays(1)
                });
            }
            if (task.Status == "Completed Pending Review" || task.Status == "Completed")
            {
                task.Histories.Add(new TaskHistory
                {
                    Employee = task.Status == "Completed" ? task.CreatedByEmployee : task.AssignedEmployee,
                    Action = task.Status == "Completed" ? "Review" : "Progress Report",
                    Details = task.Status == "Completed" ? "成果物を確認し、完了として承認しました。" : "完了報告を提出し、レビュー待ちになりました。",
                    CreatedAt = task.UpdatedAt
                });
            }
            if (task.Status == "Completed")
            {
                db.JobProgressLogs.Add(new JobProgressLog
                {
                    Task = task, User = task.CreatedByEmployee.User, Status = "approved", Percent = 100,
                    Note = "成果物を確認し、完了として承認しました。", CreatedAt = task.CompletedDate.Value
                });
            }
            db.TaskComments.Add(new TaskComment
            {
                Task = task, Employee = task.AssignedEmployee, Comment = "作業内容を確認しました。関連資料を参照して対応します。",
                CreatedAt = task.CreatedAt.AddHours(6)
            });
            if (index < 50)
            {
                db.TaskComments.Add(new TaskComment
                {
                    Task = task, Employee = task.CreatedByEmployee,
                    Comment = index % 3 == 0 ? "懸念点があれば早めに共有してください。" : "対応方針で問題ありません。完了後に結果を記録してください。",
                    CreatedAt = task.CreatedAt.AddHours(12)
                });
            }
        }
    }

    private static void CreateDeadlineRequests(ApplicationDbContext db, IList<EmployeeTaskManagement.Models.Task> tasks, ApplicationUser admin)
    {
        for (var index = 0; index < 12; index++)
        {
            var task = tasks[25 + index];
            var original = DateTime.UtcNow.Date.AddDays(6 + index);
            var requested = original.AddDays(5);
            if (task.Project.DueDate.HasValue && requested > task.Project.DueDate.Value.Date)
            {
                requested = task.Project.DueDate.Value.Date;
                original = requested.AddDays(-5);
            }
            var status = index < 4 ? "Pending" : index < 9 ? "Approved" : "Rejected";
            task.DueDate = status == "Approved" ? requested : original;
            db.TaskDeadlineRequests.Add(new TaskDeadlineRequest
            {
                Task = task, RequestedBy = task.AssignedEmployee.User, RequestedByUserId = task.AssignedEmployee.UserId,
                RequestedDueDate = requested,
                Reason = index % 2 == 0 ? "外部仕様の確定が遅れたため、レビュー期間を確保したいです。" : "関連部署との確認に追加日数が必要です。",
                Status = status, DecidedBy = status == "Pending" ? null : admin,
                DecisionNote = status == "Approved" ? "影響範囲を確認し、変更後の期限を承認します。"
                    : status == "Rejected" ? "後続工程への影響が大きいため、現行期限内で対応してください。" : null,
                CreatedAt = task.CreatedAt.AddDays(2), DecidedAt = status == "Pending" ? null : (DateTime?)task.CreatedAt.AddDays(3)
            });
        }
    }

    private static IList<ProjectProgressReport> CreateReports(ApplicationDbContext db, IList<Project> projects,
        IList<EmployeeTaskManagement.Models.Task> tasks, IDictionary<Project, IList<Employee>> memberLookup)
    {
        var result = new List<ProjectProgressReport>();
        foreach (var task in tasks.Skip(40).Take(20))
        {
            var report = new ProjectProgressReport
            {
                Project = task.Project, Task = task, Reporter = task.AssignedEmployee.User, ReporterUserId = task.AssignedEmployee.UserId,
                Recipient = task.Project.ProjectManager, TaskCompleted = false, ReportType = "TaskUpdate",
                Note = "現在の進捗は" + task.Progress + "%です。予定どおり進めています。", CreatedAt = task.CreatedAt.AddDays(2)
            };
            db.ProjectProgressReports.Add(report); result.Add(report);
        }
        foreach (var project in projects.Where(item => item.Status != "Not Started").Take(5))
        {
            var reporter = memberLookup[project].Last();
            var report = new ProjectProgressReport
            {
                Project = project, Reporter = reporter.User, ReporterUserId = reporter.UserId, Recipient = project.ProjectManager,
                ReportType = "ProjectSuggestion", Note = "次回定例では、未確定仕様とリスク項目を先に確認することを提案します。",
                CreatedAt = project.CreatedAt.AddDays(30)
            };
            db.ProjectProgressReports.Add(report); result.Add(report);
        }
        return result;
    }

    private static void CreateAttachments(ApplicationDbContext db, string webRoot,
        IList<EmployeeTaskManagement.Models.Task> tasks, IList<ProjectProgressReport> reports, ICollection<string> createdFiles)
    {
        var jobDirectory = Path.Combine(webRoot, "App_Data", "JobAttachments");
        var reportDirectory = Path.Combine(webRoot, "App_Data", "ProjectReportAttachments");
        Directory.CreateDirectory(jobDirectory); Directory.CreateDirectory(reportDirectory);
        for (var index = 0; index < 4; index++)
        {
            var name = "sokai-demo-" + Guid.NewGuid().ToString("N") + ".pdf";
            var path = Path.Combine(jobDirectory, name); WritePdf(path, "SOKAI task evidence " + (index + 1)); createdFiles.Add(path);
            db.JobAttachments.Add(new JobAttachment
            {
                Task = tasks[80 + index], FileName = "作業確認資料_" + (index + 1) + ".pdf", StoredFileName = name,
                MimeType = "application/pdf", SizeBytes = new FileInfo(path).Length,
                UploadedBy = tasks[80 + index].AssignedEmployee.User, UploadedAt = tasks[80 + index].UpdatedAt
            });
        }
        for (var index = 0; index < 2; index++)
        {
            var name = "sokai-demo-" + Guid.NewGuid().ToString("N") + ".pdf";
            var path = Path.Combine(reportDirectory, name); WritePdf(path, "SOKAI progress report " + (index + 1)); createdFiles.Add(path);
            db.ProjectReportAttachments.Add(new ProjectReportAttachment
            {
                Report = reports[index], FileName = "進捗報告資料_" + (index + 1) + ".pdf", StoredFileName = name,
                MimeType = "application/pdf", SizeBytes = new FileInfo(path).Length,
                UploadedBy = reports[index].Reporter, UploadedAt = reports[index].CreatedAt
            });
        }
    }

    private static void CreateActivityLogsIfNeeded(ApplicationDbContext db, IList<ApplicationUser> users)
    {
        var existing = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.ActivityLogs").Single();
        var needed = Math.Max(0, 120 - existing);
        var actions = new[] { "Tasks", "TaskDetails", "SubmitJobProgress", "AddProjectComment", "Notifications", "ManagerProgress" };
        for (var index = 0; index < needed; index++)
        {
            var user = users[index % users.Count];
            var action = actions[index % actions.Length];
            db.Database.ExecuteSqlCommand(
                "INSERT dbo.ActivityLogs(UserId,UserName,Controller,Action,HttpMethod,Result,IpAddress,CreatedAt) VALUES(@u,@n,@c,@a,@m,@r,@i,@d)",
                new SqlParameter("@u", user.Id), new SqlParameter("@n", user.UserName),
                new SqlParameter("@c", action == "TaskDetails" || action == "SubmitJobProgress" ? "Task" : "Home"),
                new SqlParameter("@a", action), new SqlParameter("@m", action.StartsWith("Submit") || action.StartsWith("Add") ? "POST" : "GET"),
                new SqlParameter("@r", "200"), new SqlParameter("@i", "127.0.0.1"),
                new SqlParameter("@d", DateTime.UtcNow.AddHours(-(needed - index))));
        }
    }

    private static void ValidateCounts(ApplicationDbContext db, IList<Department> departments, IList<Employee> employees,
        IList<Project> projects, IList<EmployeeTaskManagement.Models.Task> tasks, IList<ProjectProgressReport> reports)
    {
        if (departments.Count != 5 || employees.Count != 25 || projects.Count != 10 || tasks.Count != 100 || reports.Count != 25
            || db.TaskComments.Local.Count != 150 || db.TaskHistories.Local.Count != 200 || db.JobProgressLogs.Local.Count != 95
            || db.ProjectComments.Local.Count != 40 || db.TaskDeadlineRequests.Local.Count != 12
            || db.JobAttachments.Local.Count != 4 || db.ProjectReportAttachments.Local.Count != 2)
            throw new InvalidOperationException("Demo dataset count validation failed; transaction was not committed.");
    }

    private static string TaskStatus(int index)
    {
        if (index < 10) return "Not Started";
        if (index < 25) return "Pending";
        if (index < 40) return "Confirmed";
        if (index < 65) return "In Progress";
        if (index < 75) return "On Hold";
        if (index < 80) return "Completed Pending Review";
        return "Completed";
    }

    private static int TaskProgress(int index, string status)
    {
        if (status == "Not Started" || status == "Pending" || status == "Confirmed") return 0;
        if (status == "Completed" || status == "Completed Pending Review") return 100;
        return status == "On Hold" ? 35 + index % 35 : 20 + index % 70;
    }

    private static string LogStatus(string status)
    {
        if (status == "Confirmed") return "confirmed";
        if (status == "On Hold") return "on_hold";
        if (status == "Completed" || status == "Completed Pending Review") return "completed";
        return "in_progress";
    }

    private static string ProgressNote(string status)
    {
        if (status == "Confirmed") return "作業内容を確認し、担当を受諾しました。";
        if (status == "On Hold") return "関連部署からの回答待ちのため、一時保留しています。";
        if (status == "Completed" || status == "Completed Pending Review") return "実装と確認を完了し、レビューを依頼しました。";
        return "設計内容に沿って実装と確認を進めています。";
    }

    private static void Ensure(IdentityResult result, string operation)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Could not " + operation + ": " + String.Join("; ", result.Errors));
    }

    private static void WritePdf(string path, string title)
    {
        var body = "BT /F1 18 Tf 72 720 Td (" + title + ") Tj ET\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            "<< /Length " + Encoding.ASCII.GetByteCount(body) + " >>\nstream\n" + body + "endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        using (var stream = new MemoryStream())
        {
            WriteAscii(stream, "%PDF-1.4\n"); var offsets = new List<long> { 0 };
            for (var index = 0; index < objects.Length; index++)
            {
                offsets.Add(stream.Position); WriteAscii(stream, (index + 1) + " 0 obj\n" + objects[index] + "\nendobj\n");
            }
            var xref = stream.Position; WriteAscii(stream, "xref\n0 6\n0000000000 65535 f \n");
            for (var index = 1; index < offsets.Count; index++) WriteAscii(stream, offsets[index].ToString("D10") + " 00000 n \n");
            WriteAscii(stream, "trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            File.WriteAllBytes(path, stream.ToArray());
        }
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value); stream.Write(bytes, 0, bytes.Length);
    }

    private static IList<PersonSeed> BuildPeople()
    {
        var names = new[]
        {
            "佐藤 美咲", "鈴木 健太", "高橋 彩乃", "田中 直樹", "伊藤 由佳", "渡辺 翔太", "山本 麻衣", "中村 大輔", "小林 さくら", "加藤 拓海",
            "吉田 奈緒", "山田 悠人", "佐々木 葵", "山口 亮", "松本 結衣", "井上 智也", "木村 明日香", "林 健一", "清水 愛", "斎藤 陸",
            "阿部 優奈", "森 海斗", "池田 七海", "橋本 蓮", "石川 真央"
        };
        var positions = new[]
        {
            "システム管理者", "プロジェクトマネージャー", "プロジェクトマネージャー", "プロジェクトマネージャー", "プロジェクトマネージャー",
            "シニアエンジニア", "QAエンジニア", "バックエンドエンジニア", "フロントエンドエンジニア", "クラウドエンジニア",
            "業務アナリスト", "アプリケーションエンジニア", "テストエンジニア", "データエンジニア", "UI/UXデザイナー",
            "インフラエンジニア", "品質管理担当", "セキュリティエンジニア", "人事担当", "Webエンジニア",
            "カスタマーサポート担当", "データベースエンジニア", "総務担当", "モバイルエンジニア", "経理担当"
        };
        var result = new List<PersonSeed>();
        for (var index = 0; index < names.Length; index++)
        {
            result.Add(new PersonSeed
            {
                Name = names[index], Position = positions[index], Role = index == 0 ? "Admin" : index < 5 ? "Manager" : "User",
                Email = index == 0 ? "sokai.admin@example.com" : index < 5
                    ? "sokai.manager" + index.ToString("D2") + "@example.com"
                    : "sokai.user" + (index - 4).ToString("D2") + "@example.com",
                Phone = "03-0000-" + (1001 + index)
            });
        }
        return result;
    }

    private sealed class PersonSeed
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Position { get; set; }
        public string Role { get; set; }
    }
}
