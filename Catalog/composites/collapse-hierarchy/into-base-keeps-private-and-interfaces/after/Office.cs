using System.Collections.Generic;

namespace Staff
{
    public class Office
    {
        private readonly List<Employee> _team = new List<Employee>();

        public void Hire(string name)
        {
            var salesman = new Employee { Name = name };
            salesman.Record(0m);
            _team.Add(salesman);
        }
    }
}
