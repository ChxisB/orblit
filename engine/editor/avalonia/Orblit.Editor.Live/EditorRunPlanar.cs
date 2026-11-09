using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Orblit.Editor.App;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.Live;

// The 2D workspace step. Its scene holds two colored boxes, so the frame shows that the scene is drawn flat.
internal sealed partial class EditorRun
{
    const string PlanarResource = "res://planar.oscn";
    const int WheelNotches = 3;

    // Left and Right sit 80 units to either side of the middle of the frame at the start view.
    const string PlanarScene = """
        [orb_scene format=3]

        [node name="Level" type="Entity2D"]

        [node name="Left" type="Entity2D" parent="."]
        position = Vector2(-80, 0)

        [node name="LeftBox" type="ColorRect" parent="Left"]
        offset_left = -10.0
        offset_top = -10.0
        offset_right = 10.0
        offset_bottom = 10.0
        color = Color(0, 1, 0, 1)

        [node name="Right" type="Entity2D" parent="."]
        position = Vector2(80, 0)

        [node name="RightBox" type="ColorRect" parent="Right"]
        offset_left = -10.0
        offset_top = -10.0
        offset_right = 10.0
        offset_bottom = 10.0
        color = Color(0, 0, 1, 1)
        """;

    /// <summary>Writes the scene the 2D step opens. It must exist before the editor starts, because the Scenes tree lists the files found at start.</summary>
    internal static void WritePlanarScene(string project) => File.WriteAllText(Path.Combine(project, "planar.oscn"), PlanarScene);

    async Task WorksIn2D()
    {
        await Wait(() => PlanarRow() is not null, "planar.oscn in the Scenes tree");
        // Two clicks in a row are the double click that opens a file. A key press does not reach the tree, because a click leaves the focus elsewhere.
        await _input.Click(PlanarRow()!);
        await _input.Click(PlanarRow()!);
        await Wait(() => RowNamed("Right") is not null, "the planar scene in the outline");
        var level = engine.Outline()!;
        var left = level.Children.Single(node => node.Name == "Left").Handle;
        var right = level.Children.Single(node => node.Name == "Right").Handle;

        var world = FrameHash();
        await _input.Click(Tab("2D"));
        await Wait(() => FrameHash() != world, "the 2D backdrop to replace the 3D world");
        await Settle();
        Expect(!window.Toolbar.IsEffectivelyVisible, "the toolbar stayed over the 2D view");
        Expect(FrameHas((r, g, b) => g > 200 && r < 80 && b < 80) && FrameHas((r, g, b) => b > 200 && r < 80 && g < 80), "the frame holds no green box and no blue box");

        var pivot = engine.PlanarPivotOf(right)!.Value;
        await _input.Click(window, InWindow(Local(pivot)));
        Expect(window.Outline.SelectedItem is OutlineNode { Name: "Right" }, $"a click on a pivot selected {(window.Outline.SelectedItem as OutlineNode)?.Name ?? "nothing"}");
        await Settle();
        var mark = Viewport.MarkSource!()!.Value;
        Expect(Vector2.Distance(mark, pivot) < 1, $"the mark is at {mark} and the pivot is at {pivot}");

        var step = new Vector2(30, 20);
        await DragInFrame(mark, step);
        var moved = engine.PlanarPivotOf(right)!.Value - pivot;
        Expect(Vector2.Distance(moved, step) < 2, $"a drag on the mark moved the pivot by {moved}, not {step}");
        Expect(Edited, $"the title shows no unsaved edits: {window.Title}");

        var corner = new Vector2(60, (float)Viewport.Frame!.Size.Height - 60);
        var leftBefore = engine.PlanarPivotOf(left)!.Value;
        var pan = new Vector2(40, -30);
        await DragInFrame(corner, pan);
        var panned = engine.PlanarPivotOf(left)!.Value - leftBefore;
        Expect(Vector2.Distance(panned, pan) < 2, $"a drag on empty space moved the view by {panned}, not {pan}");

        var gap = Gap(left, right);
        for (var notch = 0; notch < WheelNotches; notch++)
        {
            await _input.Wheel(window, InWindow(Local(corner)), 1);
        }
        await Settle();
        var grown = Gap(left, right) / gap;
        var wanted = PlanarView.Start.Zoomed(WheelNotches).Zoom;
        Expect(MathF.Abs(grown - wanted) < 0.01f, $"{WheelNotches} wheel notches scaled the view by {grown}, not {wanted}");
        Shots.Save(window, Path.Combine(output, "planar.png"));

        await _input.Tap(window, Key.S, RawInputModifiers.Meta);
        await Settle();
        var file = File.ReadAllText(Path.Combine(project, "planar.oscn"));
        var saved = PositionOf(file.Split("\n\n").Single(part => part.Contains("name=\"Right\"")));
        Expect(Vector2.Distance(saved, new Vector2(80, 0) + step) < 0.5f, $"the file holds position {saved} for Right");
        Expect(!Edited, $"the title still shows unsaved edits: {window.Title}");

        var flat = FrameHash();
        await _input.Click(Tab("3D"));
        await Wait(() => window.Toolbar.IsEffectivelyVisible && FrameHash() != flat, "the toolbar and the 3D world to come back");
    }

    TreeViewItem? PlanarRow() => window.Scenes.Tree.GetVisualDescendants().OfType<TreeViewItem>()
        .FirstOrDefault(item => item.DataContext is FileNode { Path: PlanarResource });

    // The drag is given in frame pixels, as the engine reads it, and made in control pixels.
    Task DragInFrame(Vector2 from, Vector2 by) => DragFrom(Local(from), Local(from + by) - Local(from));

    float Gap(nint a, nint b) => Vector2.Distance(engine.PlanarPivotOf(a)!.Value, engine.PlanarPivotOf(b)!.Value);

    bool FrameHas(Func<byte, byte, byte, bool> colour)
    {
        var bytes = FrameBytes(out var rowBytes);
        for (var row = 0; row < bytes.Length / rowBytes; row++)
        {
            for (var at = row * rowBytes; at + 3 < (row + 1) * rowBytes; at += 4)
            {
                if (colour(bytes[at], bytes[at + 1], bytes[at + 2]))
                {
                    return true;
                }
            }
        }
        return false;
    }

    byte[] FrameBytes(out int rowBytes)
    {
        using var pixels = Viewport.Frame!.Lock();
        rowBytes = pixels.RowBytes;
        var bytes = new byte[pixels.RowBytes * pixels.Size.Height];
        Marshal.Copy(pixels.Address, bytes, 0, bytes.Length);
        return bytes;
    }

    static Vector2 PositionOf(string block)
    {
        var line = PositionLine().Match(block);
        Expect(line.Success, "the node has no position line");
        return new Vector2(float.Parse(line.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(line.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"position = Vector2\(([^,]+), ([^)]+)\)")]
    private static partial Regex PositionLine();
}
