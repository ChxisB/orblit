using Avalonia.Controls;

namespace Orblit.Editor.App.Shell;

/// <summary>Where a link opens. The browser in the editor, and a list in a test.</summary>
internal interface ILinkOpener
{
    void Open(Uri link);
}

/// <summary>Opens a link with the system launcher of the window a control is in. A control in no window opens nothing.</summary>
internal sealed class WindowLinks(Control owner) : ILinkOpener
{
    public void Open(Uri link) => _ = TopLevel.GetTopLevel(owner)?.Launcher.LaunchUriAsync(link);
}

/// <summary>The Help menu of the Graph workspace: the guide and the reference of every step on the site.</summary>
internal sealed class HelpMenu : MenuFlyout
{
    internal static readonly Uri Docs = new("https://orblitengine.com/docs/");
    internal static readonly Uri Guide = new("https://orblitengine.com/docs/start/your-first-graph-script/");
    internal static readonly Uri Reference = new("https://orblitengine.com/docs/classes/class_graphscript/");

    public HelpMenu(ILinkOpener links)
    {
        Items.Add(Parts.ActionItem("Your first graph", () => links.Open(Guide), note: "A five minute walk through"));
        Items.Add(Parts.ActionItem("Every step explained", () => links.Open(Reference), note: "What each step does, with an example"));
    }
}
