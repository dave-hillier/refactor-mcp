using System;

namespace Shop
{
    public class Order
    {
        public static T Larger<T>(T first, T second) where T : IComparable<T> =>
            first.CompareTo(second) >= 0 ? first : second;

        public int Biggest(int a, int b) => Larger(a, b) + Larger<int>(b, a);
    }
}
