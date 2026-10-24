namespace Orblit.Ui.Tests;

public class NodeTests
{
    private static readonly UiNode Tree = new()
    {
        Type = "column",
        Classes = "p-4",
        Children =
        [
            new UiNode { Type = "text", Text = "Title" },
            new UiNode
            {
                Type = "row",
                Children = [new UiNode { Type = "button", Text = "One" }, new UiNode { Type = "button", Text = "Two" }],
            },
        ],
    };

    private static UiNode Button(string text) => new() { Type = "button", Text = text };

    private static List<string?> TextsUnder(UiNode root, IReadOnlyList<int> path) =>
        [.. root.At(path)!.Children.Select(child => child.Text)];

    [Fact]
    public void AnEmptyPathIsTheRoot() => Assert.Equal("column", Tree.At([])!.Type);

    [Fact]
    public void APathWalksChildIndices() => Assert.Equal("One", Tree.At([1, 0])!.Text);

    [Fact]
    public void APathThatLeadsNowhereIsNullRatherThanACrash()
    {
        Assert.Null(Tree.At([9]));
        Assert.Null(Tree.At([0, 0]));
        Assert.Null(Tree.At([-1]));
    }

    [Fact]
    public void ReplacingLeavesTheOriginalAlone()
    {
        var changed = Tree.ReplaceAt([1, 1], Button("Changed"));

        Assert.Equal("Changed", changed.At([1, 1])!.Text);
        Assert.Equal("Two", Tree.At([1, 1])!.Text);
    }

    [Fact]
    public void SiblingsAreSharedRatherThanCopied()
    {
        var changed = Tree.ReplaceAt([1, 1], Button("Changed"));

        Assert.Same(Tree.Children[0], changed.Children[0]);
        Assert.Same(Tree.At([1, 0]), changed.At([1, 0]));
    }

    [Fact]
    public void ReplacingTheRootReplacesEverything() =>
        Assert.Equal("text", Tree.ReplaceAt([], new UiNode { Type = "text", Text = "Only" }).Type);

    [Fact]
    public void InsertingPutsItWhereItWasAskedFor() =>
        Assert.Equal(["One", "Middle", "Two"], TextsUnder(Tree.InsertAt([1], 1, Button("Middle")), [1]));

    [Fact]
    public void AnIndexPastTheEndAppendsRatherThanFailing() =>
        Assert.Equal("Last", Tree.InsertAt([1], 99, Button("Last")).At([1])!.Children[^1].Text);

    [Fact]
    public void RemovingTakesTheSubtreeWithIt()
    {
        var changed = Tree.RemoveAt([1]);

        Assert.Single(changed.Children);
        Assert.Equal("Title", changed.Children[0].Text);
    }

    [Fact]
    public void TheRootCannotBeRemoved() => Assert.Equal("column", Tree.RemoveAt([]).Type);

    [Fact]
    public void APathThatLeadsNowhereChangesNothing()
    {
        Assert.Equal(2, Tree.RemoveAt([9]).Children.Count);
        Assert.Equal(2, Tree.InsertAt([9], 0, new UiNode { Type = "box" }).Children.Count);
    }

    [Fact]
    public void MovingReordersAmongSiblings() =>
        Assert.Equal(["Two", "One"], TextsUnder(Tree.MoveAt([1, 1], 0), [1]));

    [Fact]
    public void WalkingGivesEveryNodeWithThePathToIt()
    {
        var seen = Tree.Walk().ToList();

        Assert.Equal(5, seen.Count);
        Assert.Empty(seen[0].Path);
        Assert.Equal([1, 1], seen[^1].Path);
        Assert.Equal("One", seen[3].Node.Text);
    }

    [Fact]
    public void EveryPathItGivesLeadsBackToItsNode()
    {
        foreach (var found in Tree.Walk())
        {
            Assert.Same(Tree.At(found.Path), found.Node);
        }
    }

    [Fact]
    public void AnElementWithNoLeftOrTopHasNotBeenPlaced()
    {
        Assert.Null(new UiNode { Type = "box" }.Placed);
        Assert.Null(new UiNode { Type = "box", Css = "padding: 4px" }.Placed);
    }

    [Fact]
    public void APlacedElementSaysWhere()
    {
        var at = new UiNode { Type = "box", Css = "left: 40px; top: 12px" }.Placed!.Value;

        Assert.Equal(40.0, at.Left);
        Assert.Equal(12.0, at.Top);
    }

    [Fact]
    public void OneAxisIsEnoughAndTheOtherReadsAsZero() =>
        Assert.Equal(0.0, new UiNode { Type = "box", Css = "top: 8px" }.Placed!.Value.Left);

    [Fact]
    public void MovingItKeepsEverythingElseAboutItsStyle()
    {
        var node = new UiNode { Type = "box", Css = "padding: 4px; left: 0px; top: 0px; color: #fff" };
        var moved = node.PlaceAt(120, 60);

        Assert.Equal(120.0, moved.Placed!.Value.Left);
        Assert.Equal(60.0, moved.Placed!.Value.Top);
        Assert.Contains("padding: 4px", moved.Css);
        Assert.Contains("color: #fff", moved.Css);
        Assert.Equal(1, moved.Css.Split("left").Length - 1);
    }

    [Fact]
    public void MovingAnElementThatWasNeverPlacedPlacesIt() =>
        Assert.Equal(10.0, new UiNode { Type = "box" }.PlaceAt(10, 20).Placed!.Value.Left);

    [Fact]
    public void ADragKeepsTheFractionSoSlowDraggingDoesNotLoseGround()
    {
        var node = new UiNode { Type = "box" }.PlaceAt(0, 0);
        for (var i = 0; i < 10; i++)
        {
            var at = node.Placed!.Value;
            node = node.PlaceAt(at.Left + 0.34, at.Top, round: false);
        }

        Assert.Equal(3.4, node.Placed!.Value.Left, 0.1);
    }

    [Fact]
    public void LettingGoLandsItOnAWholePixel()
    {
        var dragged = new UiNode { Type = "box" }.PlaceAt(10.4, 20.6, round: false);
        Assert.Equal(10.4, dragged.Placed!.Value.Left);

        var dropped = dragged.PlaceAt(dragged.Placed!.Value.Left, dragged.Placed!.Value.Top);
        Assert.Contains("left: 10px", dropped.Css);
        Assert.Contains("top: 21px", dropped.Css);
    }

    [Fact]
    public void FractionsAreRoundedSoANudgeDoesNotRewriteTheFile()
    {
        var moved = new UiNode { Type = "box" }.PlaceAt(10.4, 20.6);

        Assert.Contains("left: 10px", moved.Css);
        Assert.Contains("top: 21px", moved.Css);
    }

    [Fact]
    public void ReadsTheShapeScriptSends()
    {
        var node = UiNode.Decode("""
            {
              "type": "column",
              "class": "p-4 gap-2",
              "children": [
                {"type": "text", "text": "Score", "class": "text-sm text-slate-400"},
                {"type": "button", "text": "Again", "props": {"onPressed": "restart"}}
              ]
            }
            """);

        Assert.Equal(("column", "p-4 gap-2", 2), (node.Type, node.Classes, node.Children.Count));
        Assert.Equal("restart", node.Children[^1].HandlerFor("onPressed"));
    }

    [Fact]
    public void APlainStringIsText()
    {
        var node = UiNode.Decode("\"just words\"");
        Assert.Equal(("text", "just words"), (node.Type, node.Text));
    }

    [Fact]
    public void SomethingThatIsNotADescriptionSaysSoRatherThanThrowing()
    {
        var node = UiNode.Decode("{oh dear");
        Assert.Equal("text", node.Type);
        Assert.Contains("could not be read", node.Text);
    }

    [Fact]
    public void ADescriptionSurvivesARoundTrip()
    {
        var node = new UiNode { Type = "box", Classes = "p-2", Css = "color: #fff", Children = [new UiNode { Type = "text", Text = "hello" }] };
        var back = UiNode.FromJson(node.ToJson());

        Assert.Equal((node.Type, node.Classes, node.Css), (back.Type, back.Classes, back.Css));
        Assert.Equal("hello", back.Children.Single().Text);
    }
}
