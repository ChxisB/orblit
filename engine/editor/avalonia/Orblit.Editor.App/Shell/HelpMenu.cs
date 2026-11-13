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

/// <summary>The Help menu of the Graph workspace: the guide and the class reference on the site.</summary>
internal sealed class HelpMenu : MenuFlyout
{
    internal static readonly Uri Guide = new("https://orblitengine.com/docs/start/your-first-graph-script/");
    internal static readonly Uri Reference = new("https://orblitengine.com/docs/classes/class_graphscript/");

    public HelpMenu(ILinkOpener links)
    {
        Items.Add(Entry("Your First Graph Script", Guide, links));
        Items.Add(Entry("GraphScript Class Reference", Reference, links));
    }

    static MenuItem Entry(string header, Uri link, ILinkOpener links)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => links.Open(link);
        return item;
    }
}
