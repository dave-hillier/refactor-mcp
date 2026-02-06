namespace ECommerce;

/// <summary>
/// Pricing logic with complex inline expressions and feature flag branching.
///
/// Refactoring opportunities:
///   - introduce-variable: complex discount expression in CalculateLineTotal
///   - introduce-field: repeated magic number 0.15m (max discount cap)
///   - convert-to-static-with-parameters: CalculateShippingCost uses no instance state
///   - feature-flag-refactor: EnableNewPricingEngine branching in ApplyDynamicPricing
///   - extract-method: CalculateBulkDiscount has multiple logical sections
///   - inline-method: GetBaseMultiplier is trivial, called once
/// </summary>
public class PricingEngine
{
    private readonly AppConfig _config;

    public PricingEngine(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Contains a complex inline expression that should be broken into named variables.
    /// </summary>
    public decimal CalculateLineTotal(OrderItem item, Customer customer, bool isHolidaySeason)
    {
        // This expression is hard to read — introduce-variable candidates
        return item.UnitPrice * item.Quantity * (1 - (customer.Tier == CustomerTier.Platinum ? 0.15m : customer.Tier == CustomerTier.Gold ? 0.10m : customer.Tier == CustomerTier.Silver ? 0.05m : 0m)) * (isHolidaySeason ? 1.10m : 1.0m) * (item.Quantity >= 10 ? 0.95m : 1.0m);
    }

    /// <summary>
    /// Pure function that doesn't use any instance state — convert-to-static candidate.
    /// </summary>
    public decimal CalculateShippingCost(decimal totalWeight, string destinationCountry, bool expedited)
    {
        decimal baseCost = totalWeight switch
        {
            <= 0.5m => 3.99m,
            <= 2.0m => 7.99m,
            <= 5.0m => 12.99m,
            <= 10.0m => 18.99m,
            <= 25.0m => 29.99m,
            _ => 29.99m + (totalWeight - 25.0m) * 1.50m
        };

        decimal countryMultiplier = destinationCountry switch
        {
            "US" => 1.0m,
            "CA" or "MX" => 1.5m,
            "GB" or "DE" or "FR" => 2.0m,
            "JP" or "AU" => 2.5m,
            _ => 3.0m
        };

        decimal expeditedSurcharge = expedited ? baseCost * 0.75m : 0m;

        return Math.Round(baseCost * countryMultiplier + expeditedSurcharge, 2);
    }

    /// <summary>
    /// Feature flag branching — should be refactored to strategy pattern.
    /// </summary>
    public decimal ApplyDynamicPricing(decimal basePrice, Product product, Customer customer)
    {
        if (_config.EnableNewPricingEngine)
        {
            // New pricing: demand-based with customer loyalty
            decimal demandFactor = product.StockQuantity < 10 ? 1.20m : product.StockQuantity < 50 ? 1.05m : 1.0m;
            decimal loyaltyFactor = customer.LifetimeSpend > 10000 ? 0.90m : customer.LifetimeSpend > 5000 ? 0.95m : 1.0m;
            return Math.Round(basePrice * demandFactor * loyaltyFactor, 2);
        }
        else
        {
            // Old pricing: flat category-based markup
            decimal markup = product.Category switch
            {
                "Electronics" => 1.15m,
                "Clothing" => 1.20m,
                "Food" => 1.05m,
                _ => 1.10m
            };
            return Math.Round(basePrice * markup, 2);
        }
    }

    /// <summary>
    /// Has multiple logical phases that could be extracted.
    /// </summary>
    public decimal CalculateBulkDiscount(List<OrderItem> items, Customer customer)
    {
        // Phase 1: Calculate raw totals
        decimal rawTotal = 0m;
        int totalUnits = 0;
        foreach (var item in items)
        {
            rawTotal += item.UnitPrice * item.Quantity;
            totalUnits += item.Quantity;
        }

        // Phase 2: Determine volume discount tier
        decimal volumeDiscount;
        if (totalUnits >= 100)
            volumeDiscount = 0.20m;
        else if (totalUnits >= 50)
            volumeDiscount = 0.15m;
        else if (totalUnits >= 25)
            volumeDiscount = 0.10m;
        else if (totalUnits >= 10)
            volumeDiscount = 0.05m;
        else
            volumeDiscount = 0m;

        // Phase 3: Apply loyalty bonus on top of volume discount
        decimal loyaltyBonus = 0m;
        if (customer.Tier >= CustomerTier.Gold && totalUnits >= 25)
            loyaltyBonus = 0.03m;
        if (customer.Tier >= CustomerTier.Platinum && totalUnits >= 50)
            loyaltyBonus = 0.05m;

        decimal totalDiscount = Math.Min(volumeDiscount + loyaltyBonus, 0.25m);
        return Math.Round(rawTotal * (1 - totalDiscount), 2);
    }

    /// <summary>
    /// Trivial wrapper — inline-method candidate (called only from CalculateBulkDiscount conceptually).
    /// </summary>
    public decimal GetBaseMultiplier(string category)
    {
        return category == "Premium" ? 1.25m : 1.0m;
    }
}
