namespace Shop
{
    public class Person
    {
        public virtual string Describe() => "Person";
    }

    public class Vendor
    {
    }

    public class Manager : Person
    {
        public override string Describe() => "Manager";
    }
}
