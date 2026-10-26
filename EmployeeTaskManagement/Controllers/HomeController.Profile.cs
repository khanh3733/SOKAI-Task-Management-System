using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;

namespace EmployeeTaskManagement.Controllers
{
    public partial class HomeController
    {
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UploadAvatar(HttpPostedFileBase avatar)
        {
            var userId = User.Identity.GetUserId();
            var employee = db.Employees.FirstOrDefault(item => item.UserId == userId);
            if (employee == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Conflict, "A linked employee profile is required to upload a profile photo.");
            }

            if (avatar == null || avatar.ContentLength < 1 || avatar.ContentLength > 5 * 1024 * 1024)
            {
                TempData["WorkspaceError"] = "Choose a PNG or JPEG profile photo up to 5 MB.";
                return RedirectToAction("Profile");
            }
            var extension = Path.GetExtension(Path.GetFileName(avatar.FileName));
            var expectedMime = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg"
                : null;
            if (expectedMime == null || !String.Equals(expectedMime, avatar.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkspaceError"] = "Choose a valid PNG or JPEG profile photo.";
                return RedirectToAction("Profile");
            }

            try
            {
                avatar.InputStream.Position = 0;
                using (var source = Image.FromStream(avatar.InputStream, true, true))
                {
                    if (source.Width < 1 || source.Height < 1 || source.Width > 4096 || source.Height > 4096
                        || (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && source.RawFormat.Guid != ImageFormat.Png.Guid)
                        || (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && source.RawFormat.Guid != ImageFormat.Jpeg.Guid))
                    {
                        TempData["WorkspaceError"] = "The uploaded photo content does not match its file type or dimensions.";
                        return RedirectToAction("Profile");
                    }
                    var avatarDirectory = Server.MapPath("~/App_Data/AvatarUploads");
                    Directory.CreateDirectory(avatarDirectory);
                    var storedName = Guid.NewGuid().ToString("N") + ".png";
                    var destination = Path.Combine(avatarDirectory, storedName);
                    using (var normalized = new Bitmap(source.Width, source.Height))
                    using (var graphics = Graphics.FromImage(normalized))
                    {
                        graphics.DrawImage(source, 0, 0, source.Width, source.Height);
                        normalized.Save(destination, ImageFormat.Png);
                    }
                    employee.AvatarPath = "uploads/" + storedName;
                    db.SaveChanges();
                }
            }
            catch (ArgumentException)
            {
                TempData["WorkspaceError"] = "The uploaded profile photo is invalid.";
                return RedirectToAction("Profile");
            }
            catch (ExternalException)
            {
                TempData["WorkspaceError"] = "The profile photo could not be saved.";
                return RedirectToAction("Profile");
            }
            TempData["WorkspaceSuccess"] = "Profile photo updated.";
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public ActionResult EmployeeAvatar(int id)
        {
            var employee = db.Employees.Find(id);
            if (employee == null || employee.AvatarPath == null || !employee.AvatarPath.StartsWith("uploads/", StringComparison.Ordinal))
            {
                return HttpNotFound();
            }

            var userId = User.Identity.GetUserId();
            var currentEmployee = db.Employees.FirstOrDefault(item => item.UserId == userId);
            if (!RoleAccess.IsAdministrator(User) && (currentEmployee == null || currentEmployee.EmployeeId != id))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var storedName = Path.GetFileName(employee.AvatarPath.Substring("uploads/".Length));
            var path = Path.Combine(Server.MapPath("~/App_Data/AvatarUploads"), storedName);
            if (!System.IO.File.Exists(path))
            {
                return HttpNotFound();
            }

            return File(path, "image/png");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
