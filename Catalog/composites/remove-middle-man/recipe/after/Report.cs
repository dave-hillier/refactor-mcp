namespace Company
{
    public class Report
    {
        public string Describe(Person person)
        {
            var manager = person.Department.Manager;
            return manager.Name + ": " + person.Department.Budget(2024);
        }
    }
}
