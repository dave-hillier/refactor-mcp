namespace Shop
{
    public class Door
    {
        private bool _unlocked = false;

        public void Unlock()
        {
            _unlocked = true;
        }

        public void Toggle()
        {
            _unlocked = !_unlocked;
        }

        public string Describe()
        {
            return !_unlocked ? "locked" : "open";
        }

        public bool CanOpen() => _unlocked;
    }
}
