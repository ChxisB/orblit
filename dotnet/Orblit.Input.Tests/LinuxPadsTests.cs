namespace Orblit.Input.Tests;

public class LinuxPadsTests
{
    [Fact(Skip = LinuxRig.HasBackendReason, SkipWhen = nameof(LinuxRig.IsLinux), SkipType = typeof(LinuxRig))]
    public void TheBackendRefusesToBeBuiltOffLinux()
    {
        Assert.Throws<PlatformNotSupportedException>(() => new LinuxPads());
    }

    [Fact(Skip = LinuxRig.HasBackendReason, SkipWhen = nameof(LinuxRig.IsLinux), SkipType = typeof(LinuxRig))]
    public void OpeningPadsOffLinuxReportsNoneAndDoesNotThrow()
    {
        var frame = Pads.Open().Poll();

        Assert.Equal(0, frame.Count);
        Assert.Empty(frame.Events);
    }

    [Fact(Skip = LinuxRig.LinuxOnlyReason, SkipUnless = nameof(LinuxRig.IsLinux), SkipType = typeof(LinuxRig))]
    public void OnlyGamepadsAreOpened()
    {
        using var tree = new FakeEvdevTree();
        tree.AddPad("event2", "Pad Two");
        tree.AddKeyboard("event3");
        tree.AddOther("js0");
        tree.AddOther("mouse0");
        var backend = new LinuxPads(tree.Devices, tree.Sysfs);

        var found = backend.Scan();
        backend.Close();

        var pad = Assert.Single(found);
        Assert.Equal("Pad Two", pad.Identity.Name);
        Assert.Equal(0x28de, pad.Identity.Vendor);
        Assert.Equal(0x1205, pad.Identity.Product);
        Assert.Empty(backend.Problems);
    }

    [Fact(Skip = LinuxRig.LinuxOnlyReason, SkipUnless = nameof(LinuxRig.IsLinux), SkipType = typeof(LinuxRig))]
    public void PadsComeInEventNumberOrderNotTextOrder()
    {
        using var tree = new FakeEvdevTree();
        tree.AddPad("event10", "Pad Ten");
        tree.AddPad("event2", "Pad Two");
        var backend = new LinuxPads(tree.Devices, tree.Sysfs);

        var names = backend.Scan().Select(device => device.Identity.Name).ToList();
        backend.Close();

        Assert.Equal(["Pad Two", "Pad Ten"], names);
    }

    [Fact(Skip = LinuxRig.LinuxOnlyReason, SkipUnless = nameof(LinuxRig.IsLinux), SkipType = typeof(LinuxRig))]
    public void APadWhoseNodeCannotBeOpenedIsReportedAndSkipped()
    {
        using var tree = new FakeEvdevTree();
        tree.AddPadWithoutNode("event4", "Locked Pad");
        var backend = new LinuxPads(tree.Devices, tree.Sysfs);

        var found = backend.Scan();

        Assert.Empty(found);
        var problem = Assert.Single(backend.Problems);
        Assert.Contains("event4", problem, StringComparison.Ordinal);
    }

    [Fact(Skip = LinuxRig.LinuxOnlyReason, SkipUnless = nameof(LinuxRig.IsLinux), SkipType = typeof(LinuxRig))]
    public void AnOpenPadIsKeptAcrossScans()
    {
        using var tree = new FakeEvdevTree();
        tree.AddPad("event2", "Pad Two");
        var backend = new LinuxPads(tree.Devices, tree.Sysfs);

        var first = Assert.Single(backend.Scan());
        var second = Assert.Single(backend.Scan());
        backend.Close();

        Assert.Same(first, second);
    }

    [Fact(Skip = LinuxRig.LinuxOnlyReason, SkipUnless = nameof(LinuxRig.IsLinux), SkipType = typeof(LinuxRig))]
    public void APadThatLeavesTheListingIsClosed()
    {
        using var tree = new FakeEvdevTree();
        tree.AddPad("event2", "Pad Two");
        var backend = new LinuxPads(tree.Devices, tree.Sysfs);
        var pad = Assert.Single(backend.Scan());

        tree.Remove("event2");

        Assert.Empty(backend.Scan());
        Assert.False(pad.IsAlive);
    }

    [Fact(Skip = LinuxRig.LinuxOnlyReason, SkipUnless = nameof(LinuxRig.IsLinux), SkipType = typeof(LinuxRig))]
    public void NoSysfsMeansNoPads()
    {
        using var tree = new FakeEvdevTree();
        var backend = new LinuxPads(tree.Devices, Path.Combine(tree.Sysfs, "missing"));

        Assert.Empty(backend.Scan());
        Assert.Empty(backend.Problems);
    }
}
