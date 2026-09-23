using System;

namespace Shop
{
    public readonly struct Measure<T> where T : IFormattable
    {
        private readonly T _value;
        private readonly string _unit;

        public Measure(T value, string unit)
        {
            _value = value;
            _unit = unit;
        }

        public string Format() => _value.ToString("0.0", null) + " " + _unit;
    }
}
