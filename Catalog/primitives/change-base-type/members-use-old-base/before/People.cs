namespace Shop
{
    public class Person
    {
        public string Name;
    }

    public class Vendor
    {
        public string Company;
    }

    public class Manager : Person
    {
        public string Describe() => "Manager " + Name;
    }
}
