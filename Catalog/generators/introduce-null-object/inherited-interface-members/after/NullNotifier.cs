using System;

namespace Shop
{
    public sealed class NullNotifier : INotifier
    {
        public static readonly NullNotifier Instance = new NullNotifier();

        private NullNotifier()
        {
        }

        public event EventHandler Sent { add { } remove { } }

        public string Channel { get => default; set { } }

        public bool TrySend(string message, out int id)
        {
            id = default;
            return default;
        }

        public T Echo<T>(T value) where T : class
        {
            return default;
        }

        public void Dispose()
        {
        }
    }
}
