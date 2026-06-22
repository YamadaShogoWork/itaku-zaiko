using System.ComponentModel.DataAnnotations;

namespace Zaiko.ViewModels;

public class ClientEditViewModel
{
    public int? ClientId { get; set; }

    [Required(ErrorMessage = "取引先名は必須です")]
    public string ClientName { get; set; } = string.Empty;

    public string? FaxNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public List<ClientProductRowViewModel> Products { get; set; } = [];
}
