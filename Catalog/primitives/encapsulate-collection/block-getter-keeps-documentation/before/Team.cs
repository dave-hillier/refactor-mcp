using System.Collections.Generic;

namespace Shop
{
    public class Team
    {
        private readonly List<string> _people = new List<string>();

        /// <summary>Everyone on the team.</summary>
        public List<string> People
        {
            get { return _people; }
        }
    }

    public class Rota
    {
        public void Join(Team team, string name) => team.People.Add(name);
    }
}
