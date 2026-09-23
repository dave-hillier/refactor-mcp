namespace Shop
{
    public class Grade
    {
        public string Of(int score)
        {
            if (score >= 50)
            {
                if (score < 80)
                {
                    return "pass";
                }
                else
                {
                    return "merit";
                }
            }
            else
            {
                return "fail";
            }
        }
    }
}
