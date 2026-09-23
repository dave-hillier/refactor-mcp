namespace Shop
{
    public class Job
    {
        public virtual int Run()
        {
            return 1;
        }
    }

    public class CleanupJob : Job
    {
        public override int Run()
        {
            return 0;
        }
    }
}
