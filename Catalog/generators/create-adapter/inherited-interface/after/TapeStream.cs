namespace Shop
{
    public class TapeStream : IStream
    {
        private readonly Tape _adaptee;

        public TapeStream(Tape adaptee)
        {
            _adaptee = adaptee;
        }

        public void Write(string text) => _adaptee.Record(text);

        public string Read() => _adaptee.Play();
    }
}
