# Chuẩn hóa mã nguồn

## Chọn quản lý dự án

`Models/EmployeeEligibility.cs` dùng chung quy tắc cho danh sách ở trang Projects và action SaveProject: nhân viên đang hoạt động, có role User hoặc Manager. Một người có cả hai role chỉ xuất hiện một lần. Tài khoản cần có hồ sơ Employee liên kết. Quy tắc chọn thành viên dự án vẫn là role User.

## Cấu trúc giao diện

- `Views/Home/Workspace.cshtml` dùng chung phần tiêu đề và script cho các trang chức năng; HomeController ánh xạ từng trang đến partial tương ứng.
- `_EmployeesPage.cshtml` chứa giao diện nhân viên/tài khoản; `_ProjectsPage.cshtml` chứa giao diện dự án.
- `_ProjectSummary`, `_ProjectTaskList` và `_ProjectProgressTable` dùng chung thông tin, danh sách công việc và thống kê dự án.
- Đã bỏ 8 view bọc trùng lặp, 2 trang mẫu About/Contact cùng action tương ứng, và `_LoginPartial` không được tham chiếu.
- Giữ các chức năng tài khoản, đăng nhập và lịch sử migration đang có.

## Quy ước và kiểm tra

`.editorconfig` quy định UTF-8, CRLF, thụt lề bằng 4 dấu cách, bỏ khoảng trắng cuối dòng; XML dùng 2 dấu cách. Các nhánh điều kiện/vòng lặp C# dùng dấu ngoặc khối rõ ràng. Thư viện bên thứ ba và migration không được định dạng lại.

Trong Developer PowerShell của Visual Studio, tại thư mục solution:

```powershell
MSBuild.exe EmployeeTaskManagement.sln /t:Build /p:Configuration=Debug /p:MvcBuildViews=true
./tests/Run-Tests.ps1
node --check EmployeeTaskManagement/Scripts/home-workspace.js
```

Các kiểm tra hồi quy bao gồm role User/Manager, trạng thái nhân viên, nhiều role, thiếu role và các thuộc tính phân quyền/CSRF của action dự án. Đây là kiểm tra trên dữ liệu trong bộ nhớ và metadata controller; không ghi vào cơ sở dữ liệu.

Sau khi chạy ứng dụng, kiểm tra Projects bằng tài khoản quản trị: danh sách quản lý dự án phải có cả Employee đang hoạt động mang role User và Manager. Kiểm tra thêm Employees, My Tasks, Reports và Dashboard sau khi gom partial. Manager có liên kết My Tasks để thao tác các dự án mình phụ trách.
