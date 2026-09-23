using System.Collections.Generic;

namespace Staff
{
    public class Office
    {
        private readonly List<Salesman> _team = new List<Salesman>();

        public void Hire(string name)
        {
            var salesman = new Salesman { Name = name };
            salesman.Record(0m);
            _team.Add(salesman);
        }
    }
}
