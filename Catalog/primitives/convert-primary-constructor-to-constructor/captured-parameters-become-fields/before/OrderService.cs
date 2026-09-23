namespace Shop;

/// <summary>Places orders.</summary>
public class OrderService(IRepository repository, ILogger logger)
{
    public void Place(string order)
    {
        // Save first so a failed log still keeps the order.
        repository.Save(order);
        logger.Log("placed " + order);
    }
}
