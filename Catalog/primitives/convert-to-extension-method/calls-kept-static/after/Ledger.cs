namespace Shop
{
    public class Ledger
    {
        public long Balance { get; set; }

        public long Doubled() => Balance.Twice();

        public long FromInt(int small) => Numbers.Twice(small);

        public long Literal() => Numbers.Twice(21);
    }
}
