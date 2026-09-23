using System;

namespace Shop
{
    public class Counter
    {
        private int _value;

        public int Value => _value;

        public event Action<int> Updated;

        public void Update(int value)
        {
            _value = value;
            Updated?.Invoke(value);
        }
    }
}
