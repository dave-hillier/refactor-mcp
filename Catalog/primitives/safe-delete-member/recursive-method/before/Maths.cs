namespace Shop
{
    public static class Maths
    {
        public static int Square(int n) => n * n;

        public static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);
    }
}
