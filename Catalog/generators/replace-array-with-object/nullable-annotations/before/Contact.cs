namespace Shop
{
    public class Contact
    {
        private readonly string[] _name = new string[2];

        public Contact(string first, string last)
        {
            _name[0] = first;
            _name[1] = last;
        }

        public string Greeting() => "Dear " + _name[0] + " " + _name[1];
    }
}
