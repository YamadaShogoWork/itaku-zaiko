using System.ComponentModel.DataAnnotations;

namespace Zaiko.ViewModels;

public class UserEditViewModel
{
    public string? UserId { get; set; }

    [Required(ErrorMessage = "ユーザー名は必須です")]
    public string UserName { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "パスワードが一致しません")]
    public string? ConfirmPassword { get; set; }
}
