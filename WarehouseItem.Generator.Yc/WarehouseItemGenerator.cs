using Bogus;

namespace WarehouseItem.Generator.Yc;

/// <summary>
/// Генератор тестовых данных для товаров на складе.
/// </summary>
public static class WarehouseItemGenerator
{
    private const int MaxStockQuantity = 25000;
    private const decimal MinUnitPrice = 5m;
    private const decimal MaxUnitPrice = 250_000m;
    private const double MinUnitWeight = 0.01;
    private const double MaxUnitWeight = 250.0;
    private const int MinDimensionCm = 1;
    private const int MaxDimensionCm = 99;
    private const int MaxLastDeliveryDaysAgo = 365;

    private static readonly Faker<WarehouseItemDto> _faker = new Faker<WarehouseItemDto>("ru")
        .RuleFor(x => x.ProductName, f => f.Commerce.ProductName())
        .RuleFor(x => x.Category, f => f.Commerce.Department(1))
        .RuleFor(x => x.StockQuantity, f => f.Random.Int(0, MaxStockQuantity))
        .RuleFor(x => x.UnitPrice,
            f => Math.Round(f.Random.Decimal(MinUnitPrice, MaxUnitPrice), 2, MidpointRounding.AwayFromZero))
        .RuleFor(x => x.UnitWeight,
            f => Math.Round(f.Random.Double(MinUnitWeight, MaxUnitWeight), 2, MidpointRounding.AwayFromZero))
        .RuleFor(x => x.UnitDimensions, f =>
        {
            var a = f.Random.Int(MinDimensionCm, MaxDimensionCm);
            var b = f.Random.Int(MinDimensionCm, MaxDimensionCm);
            var c = f.Random.Int(MinDimensionCm, MaxDimensionCm);
            return $"{a:D2}x{b:D2}x{c:D2} см";
        })
        .RuleFor(x => x.IsFragile, f => f.Random.Bool(0.25f))
        .RuleFor(x => x.LastDeliveryDate, f =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var daysAgo = f.Random.Int(0, MaxLastDeliveryDaysAgo);
            return today.AddDays(-daysAgo);
        })
        .RuleFor(x => x.NextDeliveryDate, (f, dto) => dto.LastDeliveryDate.AddDays(f.Random.Int(0, 90)));

    public static WarehouseItemDto Generate(int id)
    {
        var item = _faker.Generate();
        item.Id = id;
        return item;
    }
}
