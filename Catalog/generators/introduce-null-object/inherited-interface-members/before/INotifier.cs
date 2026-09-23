using System;

namespace Shop
{
    public interface INotifier : IDisposable
    {
        event EventHandler Sent;

        string Channel { get; set; }

        bool TrySend(string message, out int id);

        T Echo<T>(T value) where T : class;
    }
}
