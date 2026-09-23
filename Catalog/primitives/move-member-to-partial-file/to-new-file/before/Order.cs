using System.Collections.Generic;

namespace Shop;

public partial class Order
{
    private readonly List<string> _lines = new List<string>();

    public bool IsEmpty => _lines.Count == 0;
}
