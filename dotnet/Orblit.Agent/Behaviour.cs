using System.Globalization;

namespace Orblit.Agent;

/// <summary>How a node answered this tick.</summary>
public enum Status
{
    /// <summary>
    /// It is still working, and wants to be asked again next tick.
    ///
    /// The one that makes a behaviour tree a tree rather than a decision table.
    /// Without it every node has to finish inside one frame, which rules out
    /// walking anywhere, waiting for anything, or playing an animation to its
    /// end.
    /// </summary>
    Running,

    /// <summary>It did what it set out to do.</summary>
    Success,

    /// <summary>It could not.</summary>
    Failure,
}

/// <summary>
/// What a tick is given to work with.
///
/// A bag rather than a typed thing on purpose: a tree is authored against one
/// game's ideas, a target, a patrol route, how frightened something is, and
/// a library that named those would be a library that only fits one game.
/// What this does own is the clock, because every node that waits needs one
/// and each of them keeping its own would be a tree that ran at several
/// speeds.
/// </summary>
public class Tick(double seconds, Dictionary<string, object?>? blackboard = null)
{
    /// <summary>How long since the last tick.</summary>
    public double Seconds { get; } = seconds;

    /// <summary>What the tree knows. Written by nodes, read by nodes, owned by neither.</summary>
    public Dictionary<string, object?> Blackboard { get; } = blackboard ?? new Dictionary<string, object?>();

    /// <summary>
    /// What is stored under <paramref name="key"/>, or <c>default</c> when it is missing or of another type.
    ///
    /// shortcut: for a value type that is zero or false, where Dart gives null.
    /// Upgrade to a <c>TryRead</c> when a tree has to tell a missing int from a zero.
    /// </summary>
    public T? Read<T>(string key) => Blackboard.TryGetValue(key, out var value) && value is T typed ? typed : default;

    public void Write(string key, object? value) => Blackboard[key] = value;
}

/// <summary>
/// One node of a behaviour tree.
///
/// Nodes are stateless; everything that has to survive a tick lives in
/// <see cref="Memory"/>, keyed by the node. That is what lets one tree be shared
/// by a hundred agents, since a patrol tree is a description of patrolling,
/// not a description of one guard patrolling, and it is why a node is a
/// constant object rather than something with fields that change.
/// </summary>
public abstract class Node
{
    public abstract Status Tick(Tick tick, Memory memory);

    /// <summary>A name for a trace. Not required to be unique.</summary>
    public virtual string Label => GetType().Name;
}

/// <summary>
/// Where a tree keeps what it is in the middle of.
///
/// One of these per agent. The keys are the nodes themselves, by identity, so
/// two copies of the same node type in one tree do not share a place.
/// </summary>
public class Memory
{
    private readonly Dictionary<Node, object?> _state = new();

    public int Length => _state.Count;

    /// <summary>What <paramref name="node"/> last stored, or <paramref name="orElse"/> if it has stored nothing.</summary>
    public T Of<T>(Node node, T orElse) => _state.TryGetValue(node, out var held) && held is T typed ? typed : orElse;

    public void Put(Node node, object? state) => _state[node] = state;

    /// <summary>Forgets what <paramref name="node"/> was in the middle of.</summary>
    public void Reset(Node node) => _state.Remove(node);

    /// <summary>
    /// Forgets everything. For an agent that has been killed, teleported, or
    /// handed a new tree.
    /// </summary>
    public void Clear() => _state.Clear();
}

internal static class Labels
{
    /// <summary>
    /// A number as Dart writes it, with the <c>.0</c> on a whole one.
    ///
    /// shortcut: Dart writes 1e-7 and 1e+21 where .NET writes 1E-07 and 1E+21,
    /// and each switches at its own size, so only 0.0001 up to below 1e15
    /// agree. A label is for a trace, so upgrade only if one is ever parsed.
    /// </summary>
    internal static string Number(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var whole = double.IsFinite(value)
            && !text.Contains('E', StringComparison.Ordinal)
            && !text.Contains('.', StringComparison.Ordinal);
        return whole ? text + ".0" : text;
    }
}

/// <summary>
/// Each child in turn, stopping at the first that fails.
///
/// "Do all of these." Open the door, walk through it, close it behind you,
/// and if the door will not open, do not walk into it.
///
/// <b>It resumes where it was.</b> A sequence whose third child returned running
/// asks the third child again next tick rather than starting from the first.
/// Restarting is the commonest thing to get wrong here, and what it produces
/// is an agent that re-opens a door it is halfway through.
/// </summary>
public class Sequence(IReadOnlyList<Node> children) : Node
{
    public IReadOnlyList<Node> Children { get; } = children;

    public override string Label => "sequence";

    public override Status Tick(Tick tick, Memory memory)
    {
        var start = memory.Of(this, 0);
        if (start >= Children.Count)
        {
            start = 0;
        }

        for (var i = start; i < Children.Count; i++)
        {
            var status = Children[i].Tick(tick, memory);
            if (status == Status.Running)
            {
                memory.Put(this, i);
                return Status.Running;
            }

            if (status == Status.Failure)
            {
                memory.Reset(this);
                return Status.Failure;
            }
        }

        memory.Reset(this);
        return Status.Success;
    }
}

/// <summary>
/// Each child in turn, stopping at the first that succeeds.
///
/// "Try these until one works." Attack if something is in range, otherwise
/// chase it, otherwise go back to patrolling: the fallback chain that most
/// of an agent's behaviour actually is.
/// </summary>
public class Selector(IReadOnlyList<Node> children) : Node
{
    public IReadOnlyList<Node> Children { get; } = children;

    public override string Label => "selector";

    public override Status Tick(Tick tick, Memory memory)
    {
        var start = memory.Of(this, 0);
        if (start >= Children.Count)
        {
            start = 0;
        }

        for (var i = start; i < Children.Count; i++)
        {
            var status = Children[i].Tick(tick, memory);
            if (status == Status.Running)
            {
                memory.Put(this, i);
                return Status.Running;
            }

            if (status == Status.Success)
            {
                memory.Reset(this);
                return Status.Success;
            }
        }

        memory.Reset(this);
        return Status.Failure;
    }
}

/// <summary>
/// Every child, every tick.
///
/// For the things an agent does at the same time as other things: walking
/// while looking around, bleeding while fighting. <see cref="SucceedAfter"/> is
/// how many have to succeed for the whole to succeed; null means all of them.
/// </summary>
public class Parallel(IReadOnlyList<Node> children, long? succeedAfter = null, long failAfter = 1) : Node
{
    public IReadOnlyList<Node> Children { get; } = children;

    public long? SucceedAfter { get; } = succeedAfter;

    /// <summary>
    /// How many failures end it. One by default, because a parallel that ignored
    /// failures would keep running a child that has already given up.
    /// </summary>
    public long FailAfter { get; } = failAfter;

    public override string Label => "parallel";

    public override Status Tick(Tick tick, Memory memory)
    {
        long succeeded = 0;
        long failed = 0;

        // Every child is ticked even once the answer is known. A parallel that
        // returned early would leave the rest of its children un-ticked this
        // frame, which for anything holding a timer means a clock that runs slow
        // whenever a sibling finishes first.
        foreach (var child in Children)
        {
            switch (child.Tick(tick, memory))
            {
                case Status.Success:
                    succeeded++;
                    break;
                case Status.Failure:
                    failed++;
                    break;
                case Status.Running:
                    break;
            }
        }

        if (failed >= FailAfter)
        {
            return Status.Failure;
        }

        return succeeded >= (SucceedAfter ?? Children.Count) ? Status.Success : Status.Running;
    }
}

/// <summary>Success for failure and failure for success. Running is left alone.</summary>
public class Invert(Node child) : Node
{
    public Node Child { get; } = child;

    public override string Label => "invert";

    public override Status Tick(Tick tick, Memory memory) => Child.Tick(tick, memory) switch
    {
        Status.Success => Status.Failure,
        Status.Failure => Status.Success,
        Status.Running => Status.Running,
        _ => throw new InvalidOperationException("A node answered with a status that does not exist."),
    };
}

/// <summary>
/// Whatever the child says, called success.
///
/// For a step that is worth trying and not worth stopping for, such as
/// reloading when there is nothing to reload with.
/// </summary>
public class Succeed(Node child) : Node
{
    public Node Child { get; } = child;

    public override string Label => "succeed";

    public override Status Tick(Tick tick, Memory memory)
    {
        var status = Child.Tick(tick, memory);
        return status == Status.Running ? Status.Running : Status.Success;
    }
}

/// <summary>The child again, up to <see cref="Times"/>, or for ever when that is null.</summary>
public class Repeat(Node child, long? times = null) : Node
{
    public Node Child { get; } = child;

    public long? Times { get; } = times;

    public override string Label => Times is null ? "repeat" : $"repeat {Times}";

    public override Status Tick(Tick tick, Memory memory)
    {
        var done = memory.Of<long>(this, 0);

        var status = Child.Tick(tick, memory);
        if (status == Status.Running)
        {
            return Status.Running;
        }

        if (status == Status.Failure)
        {
            memory.Reset(this);
            return Status.Failure;
        }

        done++;
        if (Times is { } times && done >= times)
        {
            memory.Reset(this);
            return Status.Success;
        }

        memory.Put(this, done);
        return Status.Running;
    }
}

/// <summary>
/// Nothing for a while, then success.
///
/// The clock comes from the tick rather than from a real one, so a tree runs
/// the same way at any frame rate and can be stepped by hand in a test.
/// </summary>
public class Wait(double seconds) : Node
{
    public double Seconds { get; } = seconds;

    public override string Label => $"wait {Labels.Number(Seconds)}s";

    public override Status Tick(Tick tick, Memory memory)
    {
        var waited = memory.Of<double>(this, 0) + tick.Seconds;
        if (waited >= Seconds)
        {
            memory.Reset(this);
            return Status.Success;
        }

        memory.Put(this, waited);
        return Status.Running;
    }
}

/// <summary>
/// The child, but not again for <see cref="Seconds"/> after it finishes.
///
/// What stops an agent doing the expensive thing every frame it is allowed
/// to: shouting, searching, re-planning a route.
/// </summary>
public class Cooldown(Node child, double seconds) : Node
{
    public Node Child { get; } = child;

    public double Seconds { get; } = seconds;

    public override string Label => $"cooldown {Labels.Number(Seconds)}s";

    public override Status Tick(Tick tick, Memory memory)
    {
        var left = memory.Of<double>(this, 0);
        if (left > 0)
        {
            memory.Put(this, left - tick.Seconds);
            return Status.Failure;
        }

        var status = Child.Tick(tick, memory);
        if (status != Status.Running)
        {
            memory.Put(this, Seconds);
        }

        return status;
    }
}

/// <summary>
/// The child until it finishes, giving up after <see cref="Seconds"/>.
///
/// Named for what it is rather than <c>Timeout</c>, which every test package in
/// Dart also exports. A node nobody can import beside their own test
/// framework is a node nobody uses.
/// </summary>
public class Deadline(Node child, double seconds) : Node
{
    public Node Child { get; } = child;

    public double Seconds { get; } = seconds;

    public override string Label => $"deadline {Labels.Number(Seconds)}s";

    public override Status Tick(Tick tick, Memory memory)
    {
        var spent = memory.Of<double>(this, 0) + tick.Seconds;
        if (spent > Seconds)
        {
            memory.Reset(this);
            return Status.Failure;
        }

        var status = Child.Tick(tick, memory);
        if (status == Status.Running)
        {
            memory.Put(this, spent);
            return Status.Running;
        }

        memory.Reset(this);
        return status;
    }
}

/// <summary>Something the game does, which may take more than one tick.</summary>
public class Do(string name, Func<Tick, Status> run) : Node
{
    public string Name { get; } = name;

    public Func<Tick, Status> Run { get; } = run;

    public override string Label => Name;

    public override Status Tick(Tick tick, Memory memory) => Run(tick);
}

/// <summary>Something the game does that finishes immediately.</summary>
public class Act(string name, Action<Tick> run) : Node
{
    public string Name { get; } = name;

    public Action<Tick> Run { get; } = run;

    public override string Label => Name;

    public override Status Tick(Tick tick, Memory memory)
    {
        Run(tick);
        return Status.Success;
    }
}

/// <summary>A question. Success for yes.</summary>
public class Check(string name, Func<Tick, bool> ask) : Node
{
    public string Name { get; } = name;

    public Func<Tick, bool> Ask { get; } = ask;

    public override string Label => Name;

    public override Status Tick(Tick tick, Memory memory) => Ask(tick) ? Status.Success : Status.Failure;
}

/// <summary>
/// A tree and one agent's place in it.
///
/// The tree is shared and this is not: a hundred guards patrolling run one
/// tree and a hundred of these.
/// </summary>
public class Brain(Node root)
{
    private Status? _last;

    public Node Root { get; } = root;

    public Memory Memory { get; } = new();

    /// <summary>What the tree said last time it was asked.</summary>
    public Status? Status => _last;

    public Status Tick(double seconds, Dictionary<string, object?>? blackboard = null)
    {
        var answer = Root.Tick(new Tick(seconds, blackboard), Memory);
        _last = answer;
        return answer;
    }

    /// <summary>Forgets what it was in the middle of, without forgetting the tree.</summary>
    public void Interrupt()
    {
        Memory.Clear();
        _last = null;
    }
}
