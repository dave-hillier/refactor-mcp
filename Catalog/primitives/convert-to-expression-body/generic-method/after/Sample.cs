using System;

public class Sample
{
    public T Larger<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) >= 0 ? a : b;
}
