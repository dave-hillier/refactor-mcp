public class Sample
{
    private int _counter;

    public int Pair()
    {
        int first = Next();
        return first * 10 + Next();
    }

    private int Next() => ++_counter;
}
