using Catalog.Billing;

namespace Catalog.Taxes.Rates;

// Rates are percentages.
public class Rate
{
    public Invoice AppliesTo { get; set; }

    public decimal Percent { get; set; }
}
