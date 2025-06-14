using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RefactorMCP.ConsoleApp.SyntaxRewriters
{
    internal class RefactoringOpportunityWalker
    {
        private readonly MethodMetricsWalker _methodMetrics;
        private readonly ClassMetricsWalker _classMetrics;
        private readonly UnusedMembersWalker _unusedMembers;

        public List<string> Suggestions { get; } = new();

        public RefactoringOpportunityWalker(SemanticModel? model = null, Solution? solution = null)
        {
            _methodMetrics = new MethodMetricsWalker(model);
            _classMetrics = new ClassMetricsWalker();
            _unusedMembers = new UnusedMembersWalker(model, solution);
        }

        public void Visit(SyntaxNode root)
        {
            _methodMetrics.Visit(root);
            _classMetrics.Visit(root);
            _unusedMembers.Visit(root);
        }

        public async Task PostProcessAsync()
        {
            await _unusedMembers.PostProcessAsync();
            Suggestions.AddRange(_methodMetrics.Suggestions);
            Suggestions.AddRange(_classMetrics.Suggestions);
            Suggestions.AddRange(_unusedMembers.Suggestions);
        }
    }
}
