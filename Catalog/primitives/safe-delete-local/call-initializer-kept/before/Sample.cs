public class Sample
{
    private int _loads;

    public int Run()
    {
        // Warm the cache.
        int /*^*/loaded = Load();

        return _loads;
    }

    private int Load() => ++_loads;
}
