using Shop.Pricing;

namespace Shop;

public partial class Invoice
{
    public decimal Tax(decimal net) => net * new Rate().Value;
}
