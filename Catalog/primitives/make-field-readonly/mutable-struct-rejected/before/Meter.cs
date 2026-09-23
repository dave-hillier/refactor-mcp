namespace Shop
{
    public struct Tally
    {
        public int Value;

        public void Add() => Value++;
    }

    public class Meter
    {
        private Tally _tally;

        public void Tick() => _tally.Add();

        public int Reading() => _tally.Value;
    }
}
