namespace Shop
{
    public class Document
    {
        // Whether there are unsaved changes.
        private bool _saved = true;

        public void Edit(bool changed)
        {
            _saved &= !changed;
        }

        public void Check(bool valid)
        {
            _saved |= !valid;
        }

        public void Save()
        {
            if (!_saved && Validate())
            {
                _saved = true;
            }
        }

        private bool Validate() => true;
    }
}
