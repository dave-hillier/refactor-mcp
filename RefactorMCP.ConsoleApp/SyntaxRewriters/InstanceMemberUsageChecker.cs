using RefactorMCP.ConsoleApp.SyntaxWalkers;
using System.Collections.Generic;

internal class InstanceMemberUsageChecker : TrackedNameWalker
{
    public bool HasInstanceMemberUsage => Matches.Count > 0;

    public InstanceMemberUsageChecker(HashSet<string> knownInstanceMembers)
        : base(knownInstanceMembers)
    {
    }
}
