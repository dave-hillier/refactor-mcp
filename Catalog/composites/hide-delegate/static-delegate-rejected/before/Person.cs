namespace Staff
{
    public class Company
    {
        public string Name { get; set; } = "";
    }

    public class Person
    {
        public static Company Employer { get; set; } = new Company();
    }

    public class Badge
    {
        public string Print() => Person.Employer.Name;
    }
}
