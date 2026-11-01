namespace Orblit.Save;

/// <summary>
/// How far apart the kept versions of a save must be once they are
/// <paramref name="age"/> old. A zero keeps every version. Null drops anything that old.
/// </summary>
public delegate TimeSpan? Spacing(TimeSpan age);

/// <summary>The spacings a history is pruned by.</summary>
public static class Spacings
{
    /// <summary>
    /// Every version for a day, one an hour for a week, one a day for a month, and
    /// nothing older.
    /// </summary>
    public static TimeSpan? Default(TimeSpan age)
    {
        if (age < TimeSpan.FromDays(1))
        {
            return TimeSpan.Zero;
        }

        if (age < TimeSpan.FromDays(7))
        {
            return TimeSpan.FromHours(1);
        }

        return age < TimeSpan.FromDays(30) ? TimeSpan.FromDays(1) : null;
    }
}
