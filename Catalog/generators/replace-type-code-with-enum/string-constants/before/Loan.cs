namespace Library;

public class Loan
{
    public const string Book = "B";
    public const string Film = "F";

    public Loan(string kind)
    {
        Kind = kind;
    }

    public string Kind { get; }

    public int Days()
    {
        return Kind switch
        {
            Book => 21,
            Film => 7,
            _ => 14,
        };
    }
}
