namespace Staff
{
    public struct Employee
    {
        private readonly Person _person = new Person();

        public Employee()
        {
        }

        public string LastName() => _person.LastName();
    }
}
