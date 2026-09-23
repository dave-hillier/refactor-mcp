public class Sample
{
    private int _a;
    private int _b;

    /// <summary>The sum of both parts.</summary>
    public int Total()
    {
        return _a + _b; // unchecked
    }

    public int Difference() => _a - _b;
}
