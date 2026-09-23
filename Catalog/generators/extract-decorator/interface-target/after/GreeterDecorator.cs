namespace Shop
{
    public class GreeterDecorator : IGreeter
    {
        private readonly IGreeter _inner;

        public GreeterDecorator(IGreeter inner)
        {
            _inner = inner;
        }

        public string Greet(string name) => _inner.Greet(name);
    }
}
