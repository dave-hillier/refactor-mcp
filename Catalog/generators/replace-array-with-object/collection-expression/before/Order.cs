namespace Shop;

public class Order
{
    private decimal[] _range = [0m, 100m];

    public void Widen()
    {
        _range = new decimal[2] { _range[0] - 10m, _range[1] + 10m };
    }

    public void Reset() => _range = [0m, 0m];

    public bool Contains(decimal price) => price >= _range[0] && price <= _range[1];
}
