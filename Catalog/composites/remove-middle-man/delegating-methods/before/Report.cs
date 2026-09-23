namespace Company
{
    public class Report
    {
        public string Describe(Person person)
        {
            var manager = person.GetManager();
            return manager.Name + ": " + person.Budget(2024);
        }
    }
}
