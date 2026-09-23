namespace Shop
{
    public class Fixture
    {
        public string Title()
        {
            var /*^*/teams = new[]
            {
                "Arsenal", // at home
                "Chelsea"  // visiting
            };

            // Home side first.
            return teams[0] /* home */ + " v " + teams[1];
        }
    }
}
