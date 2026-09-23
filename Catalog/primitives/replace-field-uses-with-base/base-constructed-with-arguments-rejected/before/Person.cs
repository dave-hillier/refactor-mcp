namespace Staff
{
    public class Person
    {
        public Person()
        {
        }

        public Person(string name) => Name = name;

        public string Name { get; set; } = "";
    }
}
