namespace Shop.Reports
{
    public class Scoreline
    {
        public string Describe(Result result)
        {
            return result.Score.Home + "-" + result.Score.Away;
        }

        public void Reset(Result result)
        {
            result.Score = new FinalScore { Home = 0, Away = 0 };
        }
    }
}
