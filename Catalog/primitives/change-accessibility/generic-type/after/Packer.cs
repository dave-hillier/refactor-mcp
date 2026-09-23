namespace Shop;

internal class Packer
{
    public Box<int> Pack(int value)
    {
        return new Box<int> { Item = value };
    }
}
