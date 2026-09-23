namespace Shop
{
    public class StreamDecorator : IStream
    {
        private readonly IStream _inner;

        public StreamDecorator(IStream inner)
        {
            _inner = inner;
        }

        public string Current => _inner.Current;

        public void Write(string text) => _inner.Write(text);

        object IReader.Current => ((IReader)_inner).Current;

        public string Read() => _inner.Read();

        public string Read(int count) => _inner.Read(count);
    }
}
