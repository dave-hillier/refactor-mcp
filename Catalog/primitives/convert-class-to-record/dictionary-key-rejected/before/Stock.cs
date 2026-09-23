using System.Collections.Generic;

namespace Shop;

public class Stock
{
    private readonly Dictionary<Sku, int> _levels = new();

    public void Add(Sku sku, int quantity) => _levels[sku] = quantity;
}
