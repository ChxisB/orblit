using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.KitStoreCases;

namespace Orblit.Asset.Tests;

public class KitStoreParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_asset", "kit_store");

    // The same bytes out of encode, and the same refusals, for good entries and every way to break one.
    [Fact]
    public void EntriesMatchTheFixture() =>
        Golden.Check("entry", c => Dump.Attempt(() => EntryOut(Convert.FromBase64String(Text(c, 0)), Text(c, 1), Text(c, 2))));

    [Fact]
    public void EntriesAtTheSizeLimitMatchTheFixture() =>
        Golden.Check("entryPadded", c => Dump.Attempt(() => EntryOut(PaddedEntry(In(c, 0).GetInt32()), Text(c, 1), Text(c, 2))));

    [Fact]
    public void DaysMatchTheFixture() =>
        Golden.Check("date", c => Dump.Attempt(() =>
        {
            KitText.CheckDate("released", Text(c, 0));
            return true;
        }));

    [Fact]
    public void IndexesMatchTheFixture() =>
        Golden.Check("index", c => Dump.Attempt(() => IndexOut(Convert.FromBase64String(Text(c, 0)))));

    [Fact]
    public void ReleaseListsMatchTheFixture() =>
        Golden.Check("releases", c => Dump.Attempt(() => ReleasesOut(Convert.FromBase64String(Text(c, 0)))));

    [Fact]
    public void LocksMatchTheFixture() =>
        Golden.Check("lock", c => Dump.Attempt(() => LockOut(Convert.FromBase64String(Text(c, 0)))));
}
