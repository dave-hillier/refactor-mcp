public class Sample
{
    private int _loads;

    public int Run()
    {
        var /*^*/total = Load() + 1;
        return _loads;
    }

    private int Load() => ++_loads;
}
