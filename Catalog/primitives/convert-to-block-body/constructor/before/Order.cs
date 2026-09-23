namespace Shop;

public class Entity
{
}

public class Order : Entity
{
    private readonly int _quantity;

    public Order(int quantity) : base() => _quantity = quantity;
}
