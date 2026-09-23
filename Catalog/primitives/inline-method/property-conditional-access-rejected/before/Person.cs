namespace Company
{
    public class Person
    {
        public string First { get; set; }

        public string Last { get; set; }

        public string Initials => First.Substring(0, 1) + Last.Substring(0, 1);
    }

    public class Report
    {
        public string Describe(Person person) => person?.Initials;
    }
}
