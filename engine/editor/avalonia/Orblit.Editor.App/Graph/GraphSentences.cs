using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// One line of a graph read out: how deep it sits, the words that lead into it, and the step it names by number
/// and plain name. A step met a second time reads as a way back to it.
/// </summary>
internal sealed record SentenceLine(int Depth, string Lead, int Step, string Name, bool Again)
{
    /// <summary>The line as one piece of text, such as <c>then 4 Print text</c> or <c>Back to step 2</c>.</summary>
    public string Text => Again
        ? $"{(Lead == "" ? "Back" : $"{Lead} back")} to step {Step}"
        : $"{(Lead == "" ? "" : $"{Lead} ")}{Step} {Name}";
}

/// <summary>A start read out: its function, what makes it run, and the steps it runs, in run order.</summary>
internal sealed record SentenceBlock(string Function, string Start, IReadOnlyList<SentenceLine> Lines);

/// <summary>
/// Reads a graph back as sentences, one block for each start. The walk follows the wires that say what runs next.
/// A step with one way out goes on at the same depth. A loop runs its body one deeper and goes on after it. Every
/// other step with more than one way out reads each way one deeper, led by the way's name, so a branch reads as
/// If, then, Otherwise.
/// </summary>
internal static class GraphSentences
{
    const string Entry = "entry";

    /// <summary>A block for each function of the document that is one of these starts, in the document's order.</summary>
    public static IReadOnlyList<SentenceBlock> Of(GraphDocument document, IReadOnlySet<string> starts) =>
    [
        .. document.Functions
            .Where(function => starts.Contains(function.Name))
            .Select(function => new SentenceBlock(function.Name, StartNames.Of(function.Name)?.Sentence ?? function.Name, new Reader(function).Lines())),
    ];

    sealed class Reader(GraphFunction function)
    {
        readonly List<SentenceLine> _lines = [];
        readonly HashSet<int> _met = [];

        public IReadOnlyList<SentenceLine> Lines()
        {
            if (function.Steps.FirstOrDefault(step => step.Kind == Entry) is { } entry)
            {
                _met.Add(entry.Id);
                Next(entry, 0);
            }

            return _lines;
        }

        // Reads a step and what runs after it. A step read already is said as a way back, which also ends a loop
        // that the wires close.
        void Read(int id, int depth, string lead)
        {
            if (function.Steps.FirstOrDefault(step => step.Id == id) is not { } step)
            {
                return;
            }

            var again = !_met.Add(id);
            _lines.Add(new SentenceLine(depth, lead, id, StepNames.NameOf(step.Kind), again));
            if (!again)
            {
                Next(step, depth);
            }
        }

        void Next(GraphStep step, int depth)
        {
            var ways = step.Outputs.Select((port, index) => (port.Name, port.Sort, Index: index)).Where(way => way.Sort == GraphPortSort.Flow).ToList();
            var body = ways.FindIndex(way => way.Name == "body");
            var done = ways.FindIndex(way => way.Name == "done");
            if (body >= 0 && done >= 0)
            {
                Follow(step.Id, ways[body].Index, depth + 1, "");
                Follow(step.Id, ways[done].Index, depth, "");
                return;
            }

            if (ways.Count == 1)
            {
                Follow(step.Id, ways[0].Index, depth, "");
                return;
            }

            foreach (var way in ways)
            {
                Follow(step.Id, way.Index, depth + 1, way.Name == "else" ? "Otherwise," : way.Name);
            }
        }

        void Follow(int step, int port, int depth, string lead)
        {
            foreach (var wire in function.Wires.Where(wire => wire.FromStep == step && wire.FromPort == port))
            {
                Read(wire.ToStep, depth, lead);
            }
        }
    }
}
