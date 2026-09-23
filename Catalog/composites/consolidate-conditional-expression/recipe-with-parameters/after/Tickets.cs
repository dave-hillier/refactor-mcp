namespace Venue
{
    public static class Tickets
    {
        public static decimal Fee(int age, bool member)
        {
            var senior = age >= 65;
            if (IsExempt(senior, member, age))
            {
                return 0m;
            }

            return 10m;
        }

        private static bool IsExempt(bool senior, bool member, int age)
        {
            return senior || member && age < 18;
        }
    }
}
