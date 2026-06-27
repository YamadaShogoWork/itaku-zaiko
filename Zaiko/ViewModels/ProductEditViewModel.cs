using System.ComponentModel.DataAnnotations;

namespace Zaiko.ViewModels;

public class ProductEditViewModel
{
    public string? OriginalProductName { get; set; }

    [Required(ErrorMessage = "品名は必須です")]
    public string ProductName { get; set; } = string.Empty;

    [Required(ErrorMessage = "上代は必須です")]
    [Range(0, int.MaxValue, ErrorMessage = "上代は0以上の整数を入力してください")]
    public int RetailPrice { get; set; }

    [Required(ErrorMessage = "掛け率は必須です")]
    public decimal CommissionRate { get; set; } = 0.8m;

    public decimal OriginalCommissionRate { get; set; }
    public bool HasClientProducts { get; set; }

    public bool HasColorVariants { get; set; } = true;
    public int[] SelectedColorIds { get; set; } = [];
    public bool UpdateClientProducts { get; set; }

    public List<ColorSelectItem> AllColors { get; set; } = [];
}

public class ColorSelectItem
{
    public int ColorId { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public bool HasRelatedData { get; set; }
}
