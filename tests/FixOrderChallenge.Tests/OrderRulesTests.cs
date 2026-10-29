using System.Globalization;
using FixOrderChallenge.Common;

namespace FixOrderChallenge.Tests;

public class OrderRulesTests
{
    [Theory]
    [InlineData("PETR4", 1, 0.01)]
    [InlineData("VALE3", 99_999, 999.99)]
    [InlineData("VIIA4", 500, 12.30)]
    public void Valid_orders_have_no_errors(string symbol, int quantity, double price)
    {
        var errors = OrderRules.Validate(symbol, OrderSide.Buy, quantity, (decimal)price);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ITUB4")]
    [InlineData("petr4")]
    public void Unknown_symbol_is_invalid(string? symbol)
    {
        var errors = OrderRules.Validate(symbol, OrderSide.Sell, 100, 10m);

        Assert.Equal(["symbol"], errors.Keys);
    }

    [Fact]
    public void Missing_side_is_invalid()
    {
        var errors = OrderRules.Validate("PETR4", null, 100, 10m);

        Assert.Equal(["side"], errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100000")]
    [InlineData("10.5")]
    public void Invalid_quantity(string? quantity)
    {
        var errors = OrderRules.Validate("PETR4", OrderSide.Buy, quantity is null ? null : decimal.Parse(quantity, CultureInfo.InvariantCulture), 10m);

        Assert.Equal(["quantity"], errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1000")]
    [InlineData("10.001")]
    public void Invalid_price(string? price)
    {
        var errors = OrderRules.Validate("PETR4", OrderSide.Buy, 100, price is null ? null : decimal.Parse(price, CultureInfo.InvariantCulture));

        Assert.Equal(["price"], errors.Keys);
    }
}
