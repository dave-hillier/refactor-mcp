namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Note() => _address._note ?? "none";

        public void Annotate(string? note) => _address._note = note;
    }

    public class Address
    {
        internal string? _note;
    }
}
