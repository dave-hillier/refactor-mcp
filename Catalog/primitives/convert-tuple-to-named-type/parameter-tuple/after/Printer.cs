namespace Shop;

internal class Printer
{
    public string Print(OrderLine line) => line.Name + " x" + line.Quantity;

    public string Sample() => Print(new OrderLine("tea", 2)) + Print(line: new OrderLine("cake", 1));
}

internal readonly record struct OrderLine(string Name, int Quantity);
