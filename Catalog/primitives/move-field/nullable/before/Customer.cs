namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();
        private string? _note;

        public string Note() => _note ?? "none";

        public void Annotate(string? note) => _note = note;
    }

    public class Address
    {
    }
}
