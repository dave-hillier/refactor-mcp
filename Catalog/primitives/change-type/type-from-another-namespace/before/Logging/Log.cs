namespace Shop.Logging
{
    public interface ILog
    {
        void Info(string message);
    }

    public class ConsoleLog : ILog
    {
        public void Info(string message)
        {
        }
    }
}
