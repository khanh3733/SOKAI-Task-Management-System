using System;
using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskManagement.Models
{
    public enum EmployeeGender
    {
        Male = 0, Female = 1
    }

    public class CreateUserAccountViewModel
    {
        [Required(ErrorMessage = "氏名を入力してください。"), StringLength(100, ErrorMessage = "氏名は100文字以内で入力してください。")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "ユーザー名を入力してください。"), StringLength(50, MinimumLength = 3, ErrorMessage = "ユーザー名は3～50文字で入力してください。")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "ユーザー名は半角英数字と . _ - のみ使用できます。")]
        public string Username { get; set; }
        [Required(ErrorMessage = "メールアドレスを入力してください。"), StringLength(256, ErrorMessage = "メールアドレスは256文字以内で入力してください。"), EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください。")]
        public string Email { get; set; }
        [Phone(ErrorMessage = "電話番号の形式を確認してください。"), StringLength(30, ErrorMessage = "電話番号は30文字以内で入力してください。")]
        public string Phone { get; set; }
        [Required(ErrorMessage = "パスワードを入力してください。"), MinLength(8, ErrorMessage = "パスワードは8文字以上で入力してください。")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$", ErrorMessage = "パスワードは英大文字・小文字・数字・記号を含む8文字以上で入力してください。")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Required(ErrorMessage = "確認用パスワードを入力してください。"), DataType(DataType.Password), Compare("Password", ErrorMessage = "パスワードと確認用パスワードが一致しません。")]
        public string ConfirmPassword { get; set; }
        [Required(ErrorMessage = "ロールを選択してください。")]
        public string Role { get; set; }
        public int? DepartmentId { get; set; }
        [Required(ErrorMessage = "性別を選択してください。")]
        public EmployeeGender? Gender { get; set; }
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }
        [StringLength(500)]
        public string Address { get; set; }
    }

    public class UserAccountListItem
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        public bool IsLocked { get; set; }
        public Employee Employee { get; set; }
    }

    public class EditUserAccountViewModel
    {
        [Required]
        public string Id { get; set; }
        [Required, StringLength(50, MinimumLength = 3)]
        public string Username { get; set; }
        [Required, StringLength(256), EmailAddress]
        public string Email { get; set; }
        [Phone, StringLength(30)]
        public string Phone { get; set; }
        [Required]
        public string Role { get; set; }
    }
}
