using System.Collections.Generic;

namespace Shop
{
    public class Employee
    {
        protected Dictionary<string, string> Nicknames = new Dictionary<string, string>();

        /// <summary>The nickname for a name, if there is one.</summary>
        public string? FindNickname(string name)
        {
            // Nicknames are optional.
            return Nicknames.TryGetValue(name, out var nickname) ? nickname : null;
        }
    }

    public class Manager : Employee
    {
        // Grades run from one to five.
        public int Grade;
    }
}
