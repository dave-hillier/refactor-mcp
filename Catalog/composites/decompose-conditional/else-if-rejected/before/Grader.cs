namespace School
{
    public class Grader
    {
        public string Grade(int mark)
        {
            /*^*/if (mark >= 70)
                return "distinction";
            else if (mark >= 40)
                return "pass";
            else
                return "fail";
        }
    }
}
