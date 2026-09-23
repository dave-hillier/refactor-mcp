using System;

namespace Shop
{
    public static class Compare
    {
        public static T Larger<T>(T first, T second) where T : IComparable<T> =>
            first.CompareTo(second) >= 0 ? first : second;
    }
}
