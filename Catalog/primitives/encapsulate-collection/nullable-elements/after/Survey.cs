using System.Collections.Generic;

namespace Shop
{
    public class Survey
    {
        private readonly List<string?> _answers = new List<string?>();

        public IReadOnlyList<string?> Answers => _answers.AsReadOnly();

        public void AddAnswer(string? answer) => _answers.Add(answer);

        public bool RemoveAnswer(string? answer) => _answers.Remove(answer);

        public void Skip() => AddAnswer(null);
    }
}
