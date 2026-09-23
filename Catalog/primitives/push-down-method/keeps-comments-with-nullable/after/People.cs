namespace Shop
{
    public class Employee
    {
        // Everyone has a name.
        public string Name = "";

        // Pay is monthly.
        public decimal Salary;
    }

    public class Salesman : Employee
    {
        public string Territory(string region) => FindTerritory(region) ?? Name;

        /// <summary>The territory for a region, if one is assigned.</summary>
        public string? FindTerritory(string region)
        {
            // Unassigned regions have no territory.
            return region == "north" ? "N1" : null;
        }
    }
}
