namespace Shop.Reports
{
    public class Scoreline
    {
        public string Describe(Result result)
        {
            return result.Score[0] + "-" + result.Score[1];
        }

        public void Reset(Result result)
        {
            result.Score = new[] { 0, 0 };
        }
    }
}
