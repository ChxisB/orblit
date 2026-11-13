using Avalonia;
using Orblit.Editor.App.Graph;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class StepLayoutTests
{
    // Three boxes of different sizes at different places.
    static readonly Placed[] Three =
    [
        new(1, new Rect(10, 0, 100, 50)),
        new(2, new Rect(40, 60, 80, 50)),
        new(3, new Rect(25, 120, 60, 50)),
    ];

    static string Show(IEnumerable<Move> moves) => string.Join(" ", moves.Select(move => $"{move.Id}:{move.By.X},{move.By.Y}"));

    [Theory]
    [InlineData("Left", "2:-30,0 3:-15,0")]
    [InlineData("Center", "1:5,0 2:-15,0 3:10,0")]
    [InlineData("Right", "1:10,0 3:35,0")]
    [InlineData("Top", "2:0,-60 3:0,-120")]
    [InlineData("Middle", "1:0,60 3:0,-60")]
    [InlineData("Bottom", "1:0,120 2:0,60")]
    // The enum is internal, so the theory passes its name.
    public void Align_moves_every_step_onto_the_edge_of_the_box_that_holds_them(string edge, string expected) =>
        Assert.Equal(expected, Show(StepLayout.Align(Three, Enum.Parse<Edge>(edge))));

    [Fact]
    public void Align_needs_two_steps()
    {
        Assert.Empty(StepLayout.Align([Three[0]], Edge.Left));
        Assert.Empty(StepLayout.Align([], Edge.Top));
    }

    [Fact]
    public void Distribute_gives_the_gaps_one_size_and_leaves_the_first_and_last_where_they_are()
    {
        Placed[] row = [new(3, new Rect(400, 0, 100, 50)), new(1, new Rect(0, 0, 100, 50)), new(2, new Rect(130, 0, 50, 50))];

        var moves = StepLayout.Distribute(row, Axis.Horizontal);

        Assert.Equal("2:95,0", Show(moves));
    }

    [Fact]
    public void Distribute_down_moves_along_y()
    {
        Placed[] column = [new(1, new Rect(0, 0, 50, 100)), new(2, new Rect(0, 130, 50, 50)), new(3, new Rect(0, 400, 50, 100))];

        Assert.Equal("2:0,95", Show(StepLayout.Distribute(column, Axis.Vertical)));
    }

    [Fact]
    public void Distribute_needs_three_steps() => Assert.Empty(StepLayout.Distribute([Three[0], Three[1]], Axis.Horizontal));

    [Fact]
    public void Stack_puts_steps_in_a_row_from_the_leftmost_one_on_the_highest_line()
    {
        Placed[] steps = [new(1, new Rect(0, 100, 100, 50)), new(2, new Rect(300, 130, 60, 40)), new(3, new Rect(150, 0, 80, 50))];

        Assert.Equal("1:0,-100 3:-26,0 2:-72,-130", Show(StepLayout.Stack(steps, Axis.Horizontal)));
    }

    [Fact]
    public void Stack_in_a_column_lines_the_steps_up_on_the_leftmost_edge()
    {
        Placed[] steps = [new(1, new Rect(40, 0, 50, 30)), new(2, new Rect(10, 200, 50, 40))];

        Assert.Equal("1:-30,0 2:0,-146", Show(StepLayout.Stack(steps, Axis.Vertical)));
    }

    [Fact]
    public void Steps_that_start_at_the_same_place_are_taken_in_the_order_of_their_ids()
    {
        Placed[] steps = [new(2, new Rect(0, 0, 100, 50)), new(1, new Rect(0, 0, 60, 50))];

        Assert.Equal("2:84,0", Show(StepLayout.Stack(steps, Axis.Horizontal)));
    }

    [Fact]
    public void Stack_needs_two_steps() => Assert.Empty(StepLayout.Stack([Three[0]], Axis.Vertical));

    [Fact]
    public void Straighten_carries_the_move_of_one_step_down_a_chain()
    {
        Link[] chain = [new(1, 20, 2, 50), new(2, 90, 3, 70)];

        Assert.Equal("2:0,-30 3:0,-10", Show(StepLayout.Straighten(chain)));
    }

    [Fact]
    public void Straighten_follows_the_first_wire_into_a_step()
    {
        Link[] links = [new(1, 20, 3, 100), new(2, 500, 3, 100)];

        Assert.Equal("3:0,-80", Show(StepLayout.Straighten(links)));
    }

    [Fact]
    public void Straighten_leaves_a_level_wire_alone() => Assert.Empty(StepLayout.Straighten([new(1, 40, 2, 40)]));

    [Fact]
    public void Reach_follows_wires_in_both_directions_and_stops_at_other_flows()
    {
        (int, int)[] wires = [(1, 2), (2, 3), (5, 6)];

        Assert.Equal([1, 2, 3], StepLayout.Reach([3], wires).Order());
        Assert.Equal([5, 6], StepLayout.Reach([5], wires).Order());
        Assert.Equal([9], StepLayout.Reach([9], wires).Order());
    }
}
