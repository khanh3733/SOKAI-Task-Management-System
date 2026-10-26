using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public class ExternalLoginConfirmationViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }

    public class ExternalLoginListViewModel
    {
        public string ReturnUrl { get; set; }
    }

    public class SendCodeViewModel
    {
        public string SelectedProvider { get; set; }
        public System.Collections.Generic.IList<System.Web.Mvc.SelectListItem> Providers { get; set; }
        public string ReturnUrl { get; set; }
        public bool RememberMe { get; set; }
    }

    public class VerifyCodeViewModel
    {
        [Required]
        public string Provider { get; set; }
        [Required]
        public string Code { get; set; }
        public string ReturnUrl { get; set; }
        public bool RememberMe { get; set; }
        public bool RememberBrowser { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }

    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required, DataType(DataType.Password)]
        public string Password { get; set; }
        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
        public string Password { get; set; }
        [DataType(DataType.Password), Compare("Password")]
        public string ConfirmPassword { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Code { get; set; }
        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
        public string Password { get; set; }
        [DataType(DataType.Password), Compare("Password")]
        public string ConfirmPassword { get; set; }
    }
}
