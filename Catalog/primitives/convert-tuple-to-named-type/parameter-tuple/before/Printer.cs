namespace Shop;

internal class Printer
{
    public string Print((string name, int quantity) line) => line.name + " x" + line.quantity;

    public string Sample() => Print(("tea", 2)) + Print(line: (name: "cake", quantity: 1));
}
