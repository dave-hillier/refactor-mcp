namespace Library;

public class Loan
{
    public Loan(LoanKind kind)
    {
        Kind = kind;
    }

    public LoanKind Kind { get; }

    public int Days()
    {
        return Kind switch
        {
            LoanKind.Book => 21,
            LoanKind.Film => 7,
            _ => 14,
        };
    }
}
