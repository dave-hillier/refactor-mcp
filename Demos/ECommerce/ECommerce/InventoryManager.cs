namespace ECommerce;

/// <summary>
/// Inventory management with observer pattern and adapter opportunities.
///
/// Refactoring opportunities:
///   - add-observer: add an event to ReserveStock for stock-level monitoring
///   - create-adapter: adapt InventoryManager for a WarehouseApi interface
///   - rename-symbol: poorly named 'q' parameter
///   - transform-setter-to-init: StockSnapshot properties set only during creation
///   - extract-decorator: wrap CheckReorderNeeded with logging
/// </summary>
public class InventoryManager
{
    private readonly Dictionary<string, int> _stockLevels = new();
    private readonly Dictionary<string, int> _reservations = new();
    private readonly List<string> _restockQueue = new();

    public void AddProduct(string productId, int initialStock)
    {
        _stockLevels[productId] = initialStock;
        _reservations[productId] = 0;
    }

    public int GetStockLevel(string productId)
    {
        return _stockLevels.GetValueOrDefault(productId, 0);
    }

    /// <summary>
    /// Reserves stock for an order. Should raise an event when stock is low (add-observer).
    /// </summary>
    public bool ReserveStock(string productId, int q)
    {
        if (!_stockLevels.ContainsKey(productId))
            return false;

        var available = _stockLevels[productId] - _reservations.GetValueOrDefault(productId, 0);
        if (available < q)
            return false;

        _reservations[productId] = _reservations.GetValueOrDefault(productId, 0) + q;

        // Check if we need to reorder
        var remainingStock = _stockLevels[productId] - _reservations[productId];
        if (remainingStock <= 5)
        {
            _restockQueue.Add(productId);
        }

        return true;
    }

    public void ReleaseReservation(string productId, int quantity)
    {
        if (_reservations.ContainsKey(productId))
        {
            _reservations[productId] = Math.Max(0, _reservations[productId] - quantity);
        }
    }

    /// <summary>
    /// Checks if a product needs reordering — could be wrapped with a decorator for alerting.
    /// </summary>
    public bool CheckReorderNeeded(string productId)
    {
        var stock = _stockLevels.GetValueOrDefault(productId, 0);
        var reserved = _reservations.GetValueOrDefault(productId, 0);
        var available = stock - reserved;

        return available <= 10;
    }

    public StockSnapshot GetSnapshot(string productId)
    {
        return new StockSnapshot
        {
            ProductId = productId,
            TotalStock = _stockLevels.GetValueOrDefault(productId, 0),
            Reserved = _reservations.GetValueOrDefault(productId, 0),
            Available = _stockLevels.GetValueOrDefault(productId, 0) - _reservations.GetValueOrDefault(productId, 0),
            NeedsReorder = CheckReorderNeeded(productId)
        };
    }

    public List<string> GetRestockQueue() => new(_restockQueue);
}

/// <summary>
/// Properties are set only during creation — transform-setter-to-init candidates.
/// </summary>
public class StockSnapshot
{
    public string ProductId { get; set; } = "";
    public int TotalStock { get; set; }
    public int Reserved { get; set; }
    public int Available { get; set; }
    public bool NeedsReorder { get; set; }
}

/// <summary>
/// External warehouse API that InventoryManager could be adapted to.
/// </summary>
public interface IWarehouseApi
{
    bool CheckAvailability(string sku, int quantity);
    string PlaceHold(string sku, int quantity);
    void CancelHold(string holdId);
}
