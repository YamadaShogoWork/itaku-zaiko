using Microsoft.AspNetCore.Mvc.Rendering;

namespace Zaiko.ViewModels;

public class DeliveryInputViewModel
{
    public int? ClientId { get; set; }
    public string? DeliveryDate { get; set; }  // "yyyy-MM-dd" for input[type=date]
    public List<SelectListItem> Clients { get; set; } = [];
    public List<DeliveryProductGroup> Groups { get; set; } = [];
    public bool HasTable { get; set; }
    public string? ClientName { get; set; }
}

public class DeliveryProductGroup
{
    public string ProductName { get; set; } = string.Empty;
    public List<DeliveryProductRow> Rows { get; set; } = [];
}

public class DeliveryProductRow
{
    public int ProductId { get; set; }
    public string? ColorName { get; set; }
    public int CurrentStock { get; set; }
    public int? Quantity { get; set; }  // prefilled from existing delivery
}

public class DeliveryItemInput
{
    public int ProductId { get; set; }
    public int? Quantity { get; set; }
}

public class DeliverySaveViewModel
{
    public int ClientId { get; set; }
    public string DeliveryDate { get; set; } = string.Empty;  // "yyyy-MM-dd"
    public List<DeliveryItemInput> Items { get; set; } = [];
}
