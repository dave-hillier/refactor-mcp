using System.Collections.Generic;

namespace Shop;

public interface IAudit
{
    void Record(string message);
}

public class MemoryAudit : IAudit
{
    public List<string> Entries { get; } = new List<string>();

    public void Record(string message)
    {
        Entries.Add(message);
    }
}

public class NullAudit : IAudit
{
    void IAudit.Record(string message)
    {
    }
}

public class Checkout
{
    public void Pay(IAudit audit)
    {
        audit.Record("paid");
    }
}
