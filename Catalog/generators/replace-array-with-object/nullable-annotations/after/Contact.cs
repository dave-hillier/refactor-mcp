namespace Shop
{
    public class Contact
    {
        private readonly PersonName _name = new PersonName();

        public Contact(string first, string last)
        {
            _name.First = first;
            _name.Last = last;
        }

        public string Greeting() => "Dear " + _name.First + " " + _name.Last;
    }
}
