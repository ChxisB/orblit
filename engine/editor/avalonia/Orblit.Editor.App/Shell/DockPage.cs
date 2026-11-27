using Avalonia.Controls;

namespace Orblit.Editor.App.Shell;

/// <summary>One panel of a group: its name, its mark, what it shows, and the control it puts in the group's strip.</summary>
internal sealed record DockPage(string Title, string Icon, Control Content, Control? Tool = null);
