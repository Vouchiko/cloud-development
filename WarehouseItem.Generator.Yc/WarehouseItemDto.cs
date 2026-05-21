namespace WarehouseItem.Generator.Yc;

/// <summary>
/// DTO для передачи данных о товаре на складе.
/// </summary>
public sealed class WarehouseItemDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public double UnitWeight { get; set; }
    public string UnitDimensions { get; set; } = string.Empty;
    public bool IsFragile { get; set; }
    public DateOnly LastDeliveryDate { get; set; }
    public DateOnly NextDeliveryDate { get; set; }
}
