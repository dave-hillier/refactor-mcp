namespace Shop
{
    public abstract class Employee
    {
        protected string _name;
        protected string? _nickname;

        protected Employee(string name, string? nickname)
        {
            _name = name;
            _nickname = nickname;
        }
    }

    public class Manager : Employee
    {
        public Manager(string name, string? nickname)
            : base(name, nickname)
        {
        }
    }
}
