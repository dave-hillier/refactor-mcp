namespace Shop
{
    public class Door
    {
        private bool _locked = true;

        public void Unlock()
        {
            _locked = false;
        }

        public void Toggle()
        {
            _locked = !_locked;
        }

        public string Describe()
        {
            return _locked ? "locked" : "open";
        }

        public bool CanOpen() => !_locked;
    }
}
