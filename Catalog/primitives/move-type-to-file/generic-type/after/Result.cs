using System;

namespace Shop
{
    public class Result<T>
    {
        public Result(T value, Exception error)
        {
            Value = value;
            Error = error;
        }

        public T Value { get; }

        public Exception Error { get; }
    }
}
