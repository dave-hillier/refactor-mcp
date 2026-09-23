using System;

public sealed class Lease<T> : IDisposable
{
    public Lease(T value) => Value = value;

    public T Value { get; }

    public void Dispose()
    {
    }
}

public class Sample
{
    public T Use<T>(T value)
    {
        /*^*/using (Lease<T> lease = new Lease<T>(value))
        {
            return lease.Value;
        }
    }
}
