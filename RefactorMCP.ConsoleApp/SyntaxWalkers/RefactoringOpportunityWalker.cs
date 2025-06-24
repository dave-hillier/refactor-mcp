using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class RefactoringOpportunityWalker
    {
        private readonly MethodMetricsWalker _methodMetrics;
        private readonly ClassMetricsWalker _classMetrics;
        private readonly UnusedMembersWalker _unusedMembers;
        private readonly UseInterfaceWalker _useInterface;

        public List<string> Suggestions { get; } = new();

        public RefactoringOpportunityWalker(SemanticModel? model = null, Solution? solution = null)
        {
            _methodMetrics = new MethodMetricsWalker(model);
            _classMetrics = new ClassMetricsWalker();
            _unusedMembers = new UnusedMembersWalker(model, solution);
            _useInterface = new UseInterfaceWalker(model);
        }

        public void Visit(SyntaxNode root)
        {
            _methodMetrics.Visit(root);
            _classMetrics.Visit(root);
            _unusedMembers.Visit(root);
            _useInterface.Visit(root);
        }

        public async Task PostProcessAsync()
        {
            await _unusedMembers.PostProcessAsync();
            Suggestions.AddRange(_methodMetrics.Suggestions);
            Suggestions.AddRange(_classMetrics.Suggestions);
            Suggestions.AddRange(_unusedMembers.Suggestions);
            Suggestions.AddRange(_useInterface.Suggestions);
        }
    }
}
