public static class Sample
{
    private static int _calls;
    private static int _last;

    public static int Square(int value)
    {
        _calls++;
        _last = value * value;
        return _last;
    }
}
