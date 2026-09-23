public class Sample
{
    private int _loads;

    public int Run()
    {
        // Warm the cache.
        Load();

        return _loads;
    }

    private int Load() => ++_loads;
}
