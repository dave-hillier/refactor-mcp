namespace Shop
{
    public class Sample
    {
        private bool _ready;

        public bool Prepare(bool check)
        {
            return _ready = check;
        }

        public bool IsReady() => _ready;
    }
}
