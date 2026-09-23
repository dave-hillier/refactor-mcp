namespace Company
{
    public class Report
    {
        public string Describe(Person person)
        {
            return person?.Manager.Name;
        }
    }
}
