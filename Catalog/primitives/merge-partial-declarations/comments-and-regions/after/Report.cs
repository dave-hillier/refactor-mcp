namespace Shop
{
    /// <summary>A printable report.</summary>
    public class Report
    {
        // The heading printed on every page.
        public string Title { get; set; } = "";

        // Printing is kept apart from the data.
        #region Printing

        /// <summary>The report as text.</summary>
        public string Print() => Title.ToUpperInvariant();

        #endregion
    }
}
