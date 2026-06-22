namespace Zaiko.ViewModels;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsSelf { get; set; }
    public bool CanDelete { get; set; }
}
