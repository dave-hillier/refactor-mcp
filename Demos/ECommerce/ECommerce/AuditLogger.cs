namespace ECommerce;

/// <summary>
/// Simple audit logger — target class for moving FormatAuditLogEntry from OrderProcessor.
/// </summary>
public class AuditLogger
{
    private readonly List<string> _entries = new();

    public void WriteEntry(string entry)
    {
        _entries.Add(entry);
        Console.WriteLine($"[AUDIT] {entry.Split('\n').FirstOrDefault()}");
    }

    public List<string> GetEntries() => new(_entries);

    public void Clear() => _entries.Clear();
}
