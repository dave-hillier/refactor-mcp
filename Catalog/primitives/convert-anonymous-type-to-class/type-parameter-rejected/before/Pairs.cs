namespace Shop;

public static class Pairs
{
    public static object Make<T>(T first, T second) => /*^*/new { First = first, Second = second };
}
