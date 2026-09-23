using System.Collections.Generic;

namespace Shop;

public interface IScores
{
    List<int> Top();
}

public class DailyScores : IScores
{
    public List<int> Top()
    {
        return new List<int> { 9, 7 };
    }
}

public class WeeklyScores : IScores
{
    List<int> IScores.Top()
    {
        return new List<int> { 42 };
    }
}

public class Board
{
    public int Best(IScores scores)
    {
        return scores.Top()[0];
    }
}
