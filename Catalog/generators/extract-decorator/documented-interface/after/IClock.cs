using System;

namespace Shop
{
    /// <summary>Tells the time.</summary>
    public interface IClock
    {
        #region Time

        /// <summary>The current local time.</summary>
        DateTime Now { get; }

        // Callers comparing times should use this one.
        DateTime UtcNow { get; }

        #endregion
    }
}
