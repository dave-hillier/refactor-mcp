namespace Shop;

public interface IRepository
{
    void Save(string order);
}

public interface ILogger
{
    void Log(string message);
}

public static class Composition
{
    public static OrderService Create(IRepository repository, ILogger logger) => new OrderService(repository, logger: logger);
}
