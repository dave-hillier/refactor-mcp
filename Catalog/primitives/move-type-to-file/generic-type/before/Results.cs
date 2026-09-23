using System;

namespace Shop
{
    public static class Results
    {
        public static Result<T> Ok<T>(T value) => new Result<T>(value, null);
    }

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
