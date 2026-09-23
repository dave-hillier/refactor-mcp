using System;

public class Sample
{
    public string Compare(string a, string b, int x, int y)
    {
        return Larger(a, b) + Larger(x, y);
    }

    private static T Larger<T>(T a, T b) where T : IComparable<T>
    {
        return a.CompareTo(b) >= 0 ? a : b;
    }
}
