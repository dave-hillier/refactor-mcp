using System;

namespace Company
{
    public class Report
    {
        public string Describe(Person person)
        {
            Func<Employee> manager = person.GetManager;
            return manager().Name + ": " + person.Budget(2024);
        }
    }
}
