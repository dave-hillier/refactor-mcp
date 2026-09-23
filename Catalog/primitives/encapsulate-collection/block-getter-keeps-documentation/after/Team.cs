using System.Collections.Generic;

namespace Shop
{
    public class Team
    {
        private readonly List<string> _people = new List<string>();

        /// <summary>Everyone on the team.</summary>
        public IReadOnlyList<string> People
        {
            get { return _people.AsReadOnly(); }
        }

        public void AddMember(string member) => _people.Add(member);

        public bool RemoveMember(string member) => _people.Remove(member);
    }

    public class Rota
    {
        public void Join(Team team, string name) => team.AddMember(name);
    }
}
