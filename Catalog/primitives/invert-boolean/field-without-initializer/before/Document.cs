namespace Shop
{
    public class Document
    {
        // Whether there are unsaved changes.
        private bool _dirty;

        public void Edit(bool changed)
        {
            _dirty |= changed;
        }

        public void Check(bool valid)
        {
            _dirty &= valid;
        }

        public void Save()
        {
            if (_dirty && Validate())
            {
                _dirty = false;
            }
        }

        private bool Validate() => true;
    }
}
