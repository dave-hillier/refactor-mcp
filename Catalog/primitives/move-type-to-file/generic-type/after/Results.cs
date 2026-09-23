namespace Shop
{
    public static class Results
    {
        public static Result<T> Ok<T>(T value) => new Result<T>(value, null);
    }
}
