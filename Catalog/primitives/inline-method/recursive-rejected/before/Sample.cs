public class Sample
{
    public int Six() => Factorial(3);

    private int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);
}
