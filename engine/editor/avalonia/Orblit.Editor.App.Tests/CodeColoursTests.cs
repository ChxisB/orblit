using Orblit.Editor.App.Shell;

namespace Orblit.Editor.App.Tests;

public sealed class CodeColoursTests
{
    internal const string Player = """
        using Orblit;

        public partial class Player : Entity3D
        {
            [Export] public float JumpHeight { get; set; } = 4.5f;

            // Jump while the key is down.
            public override void OnFrame(double delta)
            {
                if (Input.IsActionPressed("jump"))
                {
                    Velocity = new Vector3(0, JumpHeight, 0);
                }
            }
        }
        """;

    // The kind of the stretch that holds the first place this piece of text appears.
    static CodeKind KindOf(string text, string piece)
    {
        var at = text.IndexOf(piece, StringComparison.Ordinal);
        return CodeColours.Read(text).Single(span => span.Start <= at && at < span.Start + span.Length).Kind;
    }

    [Theory]
    [InlineData("public", nameof(CodeKind.Keyword))]
    [InlineData("Player", nameof(CodeKind.Type))]
    [InlineData("Entity3D", nameof(CodeKind.Type))]
    [InlineData("Export", nameof(CodeKind.Attribute))]
    [InlineData("JumpHeight", nameof(CodeKind.Plain))]
    [InlineData("4.5f", nameof(CodeKind.Number))]
    [InlineData("// Jump", nameof(CodeKind.Comment))]
    [InlineData("OnFrame", nameof(CodeKind.Function))]
    [InlineData("Input", nameof(CodeKind.Type))]
    [InlineData("IsActionPressed", nameof(CodeKind.Function))]
    [InlineData("\"jump\"", nameof(CodeKind.String))]
    [InlineData("Vector3", nameof(CodeKind.Type))]
    [InlineData("Velocity", nameof(CodeKind.Plain))]
    public void A_word_gets_the_kind_of_its_place(string piece, string kind)
    {
        Assert.Equal(Enum.Parse<CodeKind>(kind), KindOf(Player, piece));
    }

    [Fact]
    public void The_stretches_cover_the_text_and_neighbours_of_one_kind_join()
    {
        var spans = CodeColours.Read(Player);

        Assert.Equal(0, spans[0].Start);
        Assert.All(spans.Zip(spans.Skip(1)), pair =>
        {
            Assert.Equal(pair.First.Start + pair.First.Length, pair.Second.Start);
            Assert.NotEqual(pair.First.Kind, pair.Second.Kind);
        });
        Assert.Equal(Player.Length, spans[^1].Start + spans[^1].Length);
    }

    [Fact]
    public void A_plain_string_ends_with_its_line_and_a_verbatim_one_runs_on()
    {
        Assert.Equal(CodeKind.Plain, KindOf("var a = \"open\nnext", "next"));
        Assert.Equal(CodeKind.String, KindOf("var a = @\"one\n\"\"two\"\"\" + b", "two"));
        Assert.Equal(CodeKind.Plain, KindOf("var a = @\"one\n\"\"two\"\"\" + b", "b"));
    }

    [Fact]
    public void An_escaped_quote_does_not_end_a_string_or_a_character()
    {
        Assert.Equal(CodeKind.String, KindOf("var a = \"say \\\"hi\\\" now\"; b", "now"));
        Assert.Equal(CodeKind.Plain, KindOf("var a = '\\''; b", "b"));
    }

    [Fact]
    public void A_block_comment_runs_to_its_close_and_a_name_keeps_its_underscore()
    {
        Assert.Equal(CodeKind.Comment, KindOf("a /* one\ntwo */ b", "two"));
        Assert.Equal(CodeKind.Plain, KindOf("a /* one\ntwo */ b", "b"));
        Assert.Single(CodeColours.Read("jump_height"));
    }

    [Fact]
    public void The_outline_lists_the_members_with_their_type_or_override()
    {
        var members = ScriptMember.Read(Player);

        Assert.Equal(
            new[]
            {
                new ScriptMember("JumpHeight", "float", "MemberProperty", Player.IndexOf("JumpHeight", StringComparison.Ordinal)),
                new ScriptMember("OnFrame", "override", "MethodOverride", Player.IndexOf("OnFrame", StringComparison.Ordinal)),
            },
            members);
    }

    [Fact]
    public void A_field_needs_a_modifier_so_locals_and_statements_stay_out()
    {
        const string text = """
            const int Lives = 3;
            private Vector3 _speed;
            void Go()
            {
                float step = 2;
                return Make(step);
            }
            """;

        Assert.Equal(
            new[] { ("Lives", "int", "MemberConstant"), ("_speed", "Vector3", "MemberProperty"), ("Go", "void", "MemberMethod") },
            ScriptMember.Read(text).Select(member => (member.Name, member.Detail, member.Icon)));
    }
}
