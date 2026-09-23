namespace Shop
{
    public class Signup
    {
        public Customer Register(string name) => new Customer { Name = name };
    }
}
