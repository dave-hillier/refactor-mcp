public class Sample
{
    private readonly int[] _items = new int[4];

    public int this[int index]
    {
        get
        {
            return _items[index];
        }
    }
}
