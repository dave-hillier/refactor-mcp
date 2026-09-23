namespace Shop
{
    public class Sample
    {
        private bool _done;

        public void Finish()
        {
            Mark(ref _done);
        }

        public bool IsDone() => _done;

        private static void Mark(ref bool flag)
        {
            flag = true;
        }
    }
}
