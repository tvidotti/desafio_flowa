using FixOrderChallenge.Common;
using OrderAccumulator.Exposure;

namespace FixOrderChallenge.Tests;

public class ExposureBookTests
{
    [Fact]
    public void Buy_increases_and_sell_decreases_exposure()
    {
        var book = new ExposureBook();

        book.TryApply("PETR4", OrderSide.Buy, 10.50m, 1_000);
        book.TryApply("PETR4", OrderSide.Sell, 20.00m, 200);

        Assert.Equal(10_500m - 4_000m, book.GetExposure("PETR4"));
    }

    [Fact]
    public void Exposure_is_tracked_per_symbol()
    {
        var book = new ExposureBook();

        book.TryApply("PETR4", OrderSide.Buy, 10m, 100);
        book.TryApply("VALE3", OrderSide.Sell, 50m, 10);

        Assert.Equal(1_000m, book.GetExposure("PETR4"));
        Assert.Equal(-500m, book.GetExposure("VALE3"));
        Assert.Equal(0m, book.GetExposure("VIIA4"));
    }

    [Fact]
    public void Order_reaching_exactly_the_limit_is_accepted()
    {
        var book = new ExposureBook(limit: 1_000m);

        var decision = book.TryApply("PETR4", OrderSide.Buy, 10m, 100);

        Assert.True(decision.Accepted);
        Assert.Equal(1_000m, book.GetExposure("PETR4"));
    }

    [Fact]
    public void Order_exceeding_the_limit_is_rejected_and_not_counted()
    {
        var book = new ExposureBook(limit: 1_000m);
        book.TryApply("PETR4", OrderSide.Buy, 9m, 100);

        var decision = book.TryApply("PETR4", OrderSide.Buy, 1.01m, 100);

        Assert.False(decision.Accepted);
        Assert.Equal(900m, decision.CurrentExposure);
        Assert.Equal(1_001m, decision.ProposedExposure);
        Assert.Equal(900m, book.GetExposure("PETR4"));
    }

    [Fact]
    public void Limit_applies_to_absolute_value_on_the_sell_side()
    {
        var book = new ExposureBook(limit: 1_000m);

        Assert.True(book.TryApply("VALE3", OrderSide.Sell, 10m, 100).Accepted);
        Assert.False(book.TryApply("VALE3", OrderSide.Sell, 0.01m, 1).Accepted);
        Assert.Equal(-1_000m, book.GetExposure("VALE3"));
    }

    [Fact]
    public void Opposite_side_order_can_bring_exposure_back_within_limit()
    {
        var book = new ExposureBook(limit: 1_000m);
        book.TryApply("PETR4", OrderSide.Buy, 10m, 100);

        Assert.True(book.TryApply("PETR4", OrderSide.Sell, 10m, 150).Accepted);
        Assert.Equal(-500m, book.GetExposure("PETR4"));
    }

    [Fact]
    public void Default_limit_is_one_hundred_million()
    {
        var book = new ExposureBook();

        // 99.999 × 999,99 = 99.998.000,01 cabe; a segunda ordem igual ultrapassa.
        Assert.True(book.TryApply("PETR4", OrderSide.Buy, 999.99m, 99_999).Accepted);
        Assert.False(book.TryApply("PETR4", OrderSide.Buy, 999.99m, 99_999).Accepted);
        Assert.True(book.TryApply("PETR4", OrderSide.Buy, 1m, 1_999).Accepted);
        Assert.Equal(99_999_999.01m, book.GetExposure("PETR4"));
    }

    [Fact]
    public void Concurrent_orders_never_exceed_the_limit()
    {
        var book = new ExposureBook(limit: 1_000m);

        Parallel.For(0, 1_000, _ => book.TryApply("PETR4", OrderSide.Buy, 1m, 3));

        // 333 ordens de 3 cabem em 1.000; nenhuma atualização pode se perder ou estourar o limite.
        Assert.Equal(999m, book.GetExposure("PETR4"));
    }
}
