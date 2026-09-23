namespace Staff
{
    public class Employee : Person
    {
        private readonly Person _person = new Person();

        public string Retitle(string title)
        {
            var old = _person.title;
            _person.title = title;
            return old;
        }
    }
}
