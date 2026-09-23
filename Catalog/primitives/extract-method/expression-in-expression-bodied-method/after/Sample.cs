namespace Maths
{
    public class Sample
    {
        public int Calc(int a, int b) => Product(a, b) + 1;

        private int Product(int a, int b)
        {
            return a * b;
        }
    }
}
