namespace Catalog.Billing;

// Rates are percentages.
public class Rate
{
    public Invoice AppliesTo { get; set; }

    public decimal Percent { get; set; }
}
