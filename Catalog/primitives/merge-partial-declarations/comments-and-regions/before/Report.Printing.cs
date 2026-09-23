namespace Shop;

// Printing is kept apart from the data.
public partial class Report
{
    #region Printing

    /// <summary>The report as text.</summary>
    public string Print() => Title.ToUpperInvariant();

    #endregion
}
