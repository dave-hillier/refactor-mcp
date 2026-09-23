namespace Staff
{
    public struct Badge
    {
        private readonly int _number;

        public Badge(int number) => _number = number;

        public int Number => _number;
    }
}
