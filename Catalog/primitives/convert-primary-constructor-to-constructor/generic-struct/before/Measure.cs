using System;

namespace Shop
{
    public readonly struct Measure<T>(T value, string unit) where T : IFormattable
    {
        public string Format() => value.ToString("0.0", null) + " " + unit;
    }
}
