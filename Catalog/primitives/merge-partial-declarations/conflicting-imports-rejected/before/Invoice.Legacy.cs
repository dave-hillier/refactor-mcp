using Shop.Legacy;

namespace Shop;

public partial class Invoice
{
    public decimal LegacyTax(decimal net) => net * new Rate().Value;
}
