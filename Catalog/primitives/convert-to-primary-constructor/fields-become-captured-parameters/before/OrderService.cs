namespace Shop;

/// <summary>Places orders.</summary>
public class OrderService
{
    private readonly IRepository _repository;
    private readonly ILogger _logger;

    public OrderService(IRepository repository, ILogger logger)
    {
        _repository = repository;
        this._logger = logger;
    }

    public void Place(string order)
    {
        // Save first so a failed log still keeps the order.
        _repository.Save(order);
        this._logger.Log("placed " + order);
    }
}
