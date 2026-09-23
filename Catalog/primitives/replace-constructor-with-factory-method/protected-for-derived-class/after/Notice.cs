namespace Shop;

public class Notice
{
    /// <summary>Creates a notice.</summary>
    protected Notice(string text, int priority = 0)
    {
        Text = text;
        Priority = priority;
    }

    public static Notice Create(string text, int priority = 0) => new Notice(text, priority);

    public string Text { get; }

    public int Priority { get; }
}

public class Alert : Notice
{
    public Alert(string text) : base(text, 9)
    {
    }
}
