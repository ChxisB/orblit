namespace Orblit.Save;

/// <summary>One save: the game's bytes, why they were saved and when.</summary>
public sealed class SaveRecord
{
    /// <summary>
    /// Copies <paramref name="bytes"/>, so the caller may reuse its buffer.
    /// <paramref name="at"/> keeps its milliseconds and nothing finer, which is
    /// what a file stores, and is read as UTC.
    /// </summary>
    public SaveRecord(ReadOnlySpan<byte> bytes, SaveReason reason, DateTimeOffset at)
    {
        Bytes = bytes.ToArray();
        Reason = reason;
        At = DateTimeOffset.FromUnixTimeMilliseconds(at.ToUnixTimeMilliseconds());
    }

    /// <summary>The game's bytes.</summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>Why the save was made.</summary>
    public SaveReason Reason { get; }

    /// <summary>When the save was made, in UTC.</summary>
    public DateTimeOffset At { get; }
}
