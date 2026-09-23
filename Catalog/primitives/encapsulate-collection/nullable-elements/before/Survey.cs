using System.Collections.Generic;

namespace Shop
{
    public class Survey
    {
        private readonly List<string?> _answers = new List<string?>();

        public List<string?> Answers => _answers;

        public void Skip() => Answers.Add(null);
    }
}
