namespace Shop;

public partial class Order
{
    private decimal _total = 100m;

    private decimal Discount(decimal rate) => _total * rate;
}
