public static class Sample
{
    private static int _calls;

    public static int Square(int value)
    {
        _calls++;
        int /*^*/result = value * value;
        return result;
    }
}
