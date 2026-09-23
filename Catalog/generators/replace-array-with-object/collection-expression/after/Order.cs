namespace Shop;

public class Order
{
    private PriceRange _range = new PriceRange { Low = 0m, High = 100m };

    public void Widen()
    {
        _range = new PriceRange { Low = _range.Low - 10m, High = _range.High + 10m };
    }

    public void Reset() => _range = new PriceRange { Low = 0m, High = 0m };

    public bool Contains(decimal price) => price >= _range.Low && price <= _range.High;
}
