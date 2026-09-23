public class Sample
{
    private int _counter;

    public int Pair()
    {
        return /*[*/Next()/*]*/ * 10 + Next();
    }

    private int Next() => ++_counter;
}
