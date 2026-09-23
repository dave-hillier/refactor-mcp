namespace Shop;

public partial class Order
{
    public bool IsEmpty => _lines.Count == 0;
}
