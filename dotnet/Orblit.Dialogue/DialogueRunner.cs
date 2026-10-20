using System.Collections.ObjectModel;
using System.Diagnostics;
using Orblit.Progress;

namespace Orblit.Dialogue;

/// <summary>
/// A function the game registers for a node's <c>before</c>. It gets the table as
/// it stands and returns the table the conversation goes on with, so a hook
/// that moves a quest returns what the quest book gave back. A hook that only
/// gives an item or opens a door returns the table it was given.
/// </summary>
public delegate ProgressTable Before(ProgressTable table);

/// <summary>
/// Walks one conversation for one player. It holds no screen: the game reads
/// <see cref="Current"/> and <see cref="Options"/>, draws them, and calls <see cref="Next"/> or <see cref="Choose"/>.
/// The runner starts as soon as it is made. When no variant of the dialogue
/// holds, <see cref="IsOver"/> is true at once and nothing was run. <see cref="DialogueBook.Start"/>
/// makes it, having checked the hooks.
/// The runner works on the table it was given and does not see changes made
/// to the player's table meanwhile. A game that lets other things change the
/// table during a conversation ends the conversation when they do, or reads
/// the table again before it saves <see cref="Table"/>.
/// </summary>
public sealed class DialogueRunner
{
    private readonly DialogueDefinition _dialogue;
    private readonly IReadOnlyDictionary<string, Before> _hooks;

    public DialogueRunner(DialogueDefinition dialogue, ProgressTable table, IReadOnlyDictionary<string, Before> hooks)
    {
        _dialogue = dialogue;
        _hooks = hooks;
        Table = table;
        Options = new ReadOnlyCollection<ChoiceOption>([]);
        if (dialogue.StartFor(table) is { } start)
        {
            Enter(start);
        }
    }

    /// <summary>The table with every write and hook so far. Send the player <c>table.ChangesSince(before)</c> and save the table.</summary>
    public ProgressTable Table { get; private set; }

    /// <summary>The line or choice being shown, or null when the conversation is over.</summary>
    public Node? Current { get; private set; }

    public bool IsOver => Current is null;

    /// <summary>
    /// What the player may pick at a choice, in the order of the file. A choice
    /// with conditions that fail has fewer than it was written with. Empty when
    /// <see cref="Current"/> is not a choice.
    /// </summary>
    public IReadOnlyList<ChoiceOption> Options { get; private set; }

    /// <summary>Goes on from a line. Throws <see cref="InvalidOperationException"/> anywhere else.</summary>
    public void Next()
    {
        if (Current is not LineNode line)
        {
            throw new InvalidOperationException("Only a line goes on with next.");
        }

        Enter(line.Next);
    }

    /// <summary>
    /// Picks the option at <paramref name="index"/> of <see cref="Options"/>. Throws <see cref="InvalidOperationException"/> when
    /// <see cref="Current"/> is not a choice and <see cref="ArgumentOutOfRangeException"/> for an index <see cref="Options"/> does
    /// not have.
    /// </summary>
    public void Choose(int index)
    {
        if (Current is not ChoiceNode)
        {
            throw new InvalidOperationException("Only a choice goes on with choose.");
        }

        Enter(Options[index].Next);
    }

    /// <summary>
    /// Runs from <paramref name="first"/> to the next line or choice, or to the end. The fields
    /// are written once at the end, so a hook that throws leaves the runner
    /// where it was.
    /// </summary>
    private void Enter(string? first)
    {
        var table = Table;
        Node? shown = null;
        List<ChoiceOption> offered = [];
        var at = first;
        while (at is not null)
        {
            var node = _dialogue.Nodes[at];
            if (node.Before is { } before)
            {
                table = _hooks[before](table);
            }

            switch (node)
            {
                case LineNode:
                    shown = node;
                    at = null;
                    break;
                case ChoiceNode choice:
                    offered = Offer(choice.Options, table);
                    shown = offered.Count > 0 ? node : null;
                    at = null;
                    break;
                case SetNode set:
                    table = Apply(set.Writes, table);
                    at = set.Next;
                    break;
                case JumpNode jump:
                    at = jump.Next;
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        Table = table;
        Current = shown;
        Options = new ReadOnlyCollection<ChoiceOption>([.. offered]);
    }

    private static List<ChoiceOption> Offer(IReadOnlyList<ChoiceOption> options, ProgressTable table) =>
        [.. options.Where(o => Condition.AllHold(o.When, table))];

    private static ProgressTable Apply(IReadOnlyList<Write> writes, ProgressTable table)
    {
        foreach (var write in writes)
        {
            table = table.Set(write.Name, write.Value).Table;
        }

        return table;
    }
}
