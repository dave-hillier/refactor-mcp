using System.Collections.Generic;

internal class InstanceMemberUsageChecker : IdentifierUsageWalker
{
    public bool HasInstanceMemberUsage { get; private set; }

    public InstanceMemberUsageChecker(HashSet<string> knownInstanceMembers)
        : base(knownInstanceMembers)
    {
    }

    protected override void RecordUsage(string name)
    {
        HasInstanceMemberUsage = true;
    }
}
