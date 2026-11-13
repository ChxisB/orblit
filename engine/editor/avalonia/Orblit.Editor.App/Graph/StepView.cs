using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

internal enum StepPart
{
    Header,
    Body,
    Pin,
    Value,
    Grow,
    Shrink,
}

/// <summary>What a point lands on in a step. A pin hit says which pin and a value hit says which parameter.</summary>
internal readonly record struct StepHit(StepPart Part, PinRef Pin = default, string Parameter = "");

/// <summary>
/// One step as the canvas shows it: a header, a row for each pair of pins, a row for each parameter no pin holds,
/// and a plus and a minus when the step grows pins. Every place is worked out from the step alone, so a point can
/// be asked about without a layout pass. A reroute is a small dot with an input and an output.
/// </summary>
internal sealed class StepView
{
    internal const double Width = 190;
    internal const double HeaderHeight = 26;
    internal const double RowHeight = 22;
    internal const double RerouteSize = 16;

    const double PinRadius = 5;
    const double PinGrab = 9;
    const double Corner = 6;
    const double TextInset = 14;
    const double ValueInset = 8;
    const double ButtonWidth = 24;
    const double BookmarkInset = 12;
    const double BookmarkWidth = 10;
    const double BookmarkHeight = 14;
    const double ButtonGap = 6;
    // shortcut: a value box beside an input is a fixed 52 pixels, so a long output name on the same row can
    // touch it. Measure the names and place the box from that when a step needs it.
    const double InputValueLeft = Width * 0.28;
    const double InputValueRight = Width * 0.55;
    const string ParameterOfPins = "pins";
    const string RerouteKind = "reroute";
    const string FlowSuffix = "flow";

    static readonly IBrush s_body = Fill("#262626");
    static readonly IBrush s_field = Fill("#1a1a1a");
    static readonly IBrush s_dim = new ImmutableSolidColorBrush(EditorColors.TextDim);
    static readonly IBrush s_problem = Fill("#e0605a");
    static readonly IPen s_border = new Pen(Fill("#444444"), 1).ToImmutable();
    static readonly IPen s_selected = new Pen(EditorBrushes.Accent, 2).ToImmutable();
    static readonly IPen s_broken = new Pen(s_problem, 2).ToImmutable();
    static readonly Typeface s_face = new(FontFamily.Default);
    static readonly Typeface s_bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    static readonly IBrush[] s_categories = new[] { "#7c5a2a", "#2f6a8a", "#5a7a2a", "#7a3a6a", "#2a7a6a", "#8a4a3a", "#4a4a8a", "#6a6a2a" }
        .Select(Fill).ToArray();

    readonly GraphStep _step;
    readonly IReadOnlySet<PinRef> _wired;
    readonly List<Row> _rows;
    List<Label>? _labels;

    /// <summary>One line: an input and an output that share it, or one parameter. A note is a line of words.</summary>
    readonly record struct Row(GraphPort? Input, GraphPort? Output, string Parameter, string Value, string Note);

    readonly record struct Label(FormattedText Text, Point At);

    public StepView(GraphStep step, IReadOnlySet<PinRef> wired)
    {
        _step = step;
        _wired = wired;
        IsReroute = step.Kind.StartsWith(RerouteKind, StringComparison.Ordinal);
        _rows = IsReroute ? [] : BuildRows();
    }

    public int Id => _step.Id;

    public GraphStep Step => _step;

    public bool IsReroute { get; }

    /// <summary>How far a drag has moved the step on show. The engine does not move the step until the drag ends.</summary>
    public Vector Offset { get; set; }

    public Point Origin => new(_step.X + Offset.X, _step.Y + Offset.Y);

    public double Height => IsReroute ? RerouteSize : HeaderHeight + (RowHeight * (_rows.Count + (HasGrowth ? 1 : 0)));

    public Rect Bounds => new(Origin, new Size(IsReroute ? RerouteSize : Width, Height));

    /// <summary>The name of the step with its underscores as spaces.</summary>
    public string Title => char.ToUpperInvariant(_step.Kind[0]) + _step.Kind[1..].Replace('_', ' ');

    bool HasGrowth => _step.Growth.Side != GraphGrowthSide.None;

    public Point PinCenter(bool isInput, int port)
    {
        var origin = Origin;
        var x = isInput ? origin.X : origin.X + (IsReroute ? RerouteSize : Width);
        var y = IsReroute ? origin.Y + (RerouteSize / 2) : origin.Y + HeaderHeight + (RowHeight * (port + 0.5));
        return new Point(x, y);
    }

    public PinRef? PinAt(Point point)
    {
        PinRef? best = null;
        var nearest = PinGrab;
        for (var side = 0; side < 2; side++)
        {
            var isInput = side == 0;
            var count = isInput ? _step.Inputs.Count : _step.Outputs.Count;
            for (var port = 0; port < count; port++)
            {
                var distance = Distance(PinCenter(isInput, port), point);
                if (distance <= nearest)
                {
                    nearest = distance;
                    best = new PinRef(_step.Id, port, isInput);
                }
            }
        }
        return best;
    }

    public StepHit? HitAt(Point point)
    {
        if (PinAt(point) is { } pin)
        {
            return new StepHit(StepPart.Pin, pin);
        }
        if (!Bounds.Contains(point))
        {
            return null;
        }
        if (IsReroute)
        {
            return new StepHit(StepPart.Body);
        }
        var local = point - Origin;
        return local.Y < HeaderHeight ? new StepHit(StepPart.Header) : HitBelowHeader(local);
    }

    /// <summary>The place a parameter is edited, in graph coordinates. Empty for a parameter the step does not show.</summary>
    public Rect ValueRect(string parameter)
    {
        var row = _rows.FindIndex(candidate => candidate.Parameter == parameter);
        return row < 0 ? default : FieldOf(_rows[row], row);
    }

    public string ValueOf(string parameter) => _rows.Find(candidate => candidate.Parameter == parameter).Value ?? "";

    public void Draw(DrawingContext context, bool selected, bool bookmarked)
    {
        if (IsReroute)
        {
            DrawReroute(context, selected);
            return;
        }
        var bounds = Bounds;
        var border = selected ? s_selected : !_step.Known || _step.Problem.Length > 0 ? s_broken : s_border;
        context.DrawRectangle(s_body, border, new RoundedRect(bounds, Corner));
        var header = new Rect(bounds.X, bounds.Y, bounds.Width, HeaderHeight);
        context.DrawRectangle(CategoryBrush(_step.Category), null, new RoundedRect(header, new CornerRadius(Corner, Corner, 0, 0)));
        DrawRows(context);
        foreach (var label in Labels())
        {
            context.DrawText(label.Text, new Point(Origin.X + label.At.X, Origin.Y + label.At.Y));
        }
        if (bookmarked)
        {
            context.DrawRectangle(EditorBrushes.Accent, null, new Rect(bounds.Right - BookmarkInset - BookmarkWidth, bounds.Y - 2, BookmarkWidth, BookmarkHeight));
        }
    }

    static IBrush Fill(string color) => new ImmutableSolidColorBrush(Color.Parse(color));

    static IBrush CategoryBrush(string category) => s_categories[category.Sum(letter => letter) % s_categories.Length];

    static double Distance(Point a, Point b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    static FormattedText Text(string text, double size, IBrush brush, bool bold = false) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, bold ? s_bold : s_face, size, brush);

    List<Row> BuildRows()
    {
        var rows = new List<Row>();
        var count = Math.Max(_step.Inputs.Count, _step.Outputs.Count);
        for (var i = 0; i < count; i++)
        {
            var input = i < _step.Inputs.Count ? _step.Inputs[i] : null;
            var output = i < _step.Outputs.Count ? _step.Outputs[i] : null;
            var parameter = ValueParameter(input, i);
            rows.Add(new Row(input, output, parameter, ValueText(parameter), ""));
        }
        var held = _step.Inputs.Select(port => port.Parameter).ToHashSet();
        foreach (var (name, value) in _step.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (name != ParameterOfPins && !held.Contains(name))
            {
                rows.Add(new Row(null, null, name, value, ""));
            }
        }
        if (!_step.Known || _step.Problem.Length > 0)
        {
            rows.Add(new Row(null, null, "", "", _step.Known ? _step.Problem : "Unknown step"));
        }
        return rows;
    }

    // A data input with no wire holds its own value, so it is edited in place.
    string ValueParameter(GraphPort? input, int port) =>
        input is { Sort: GraphPortSort.Data, Parameter.Length: > 0 } && !_wired.Contains(new PinRef(_step.Id, port, true)) ? input.Parameter : "";

    string ValueText(string parameter) => parameter.Length > 0 ? _step.Parameters.GetValueOrDefault(parameter, "") : "";

    Rect FieldOf(Row row, int index)
    {
        var top = Origin.Y + HeaderHeight + (RowHeight * index) + 2;
        var left = row.Input is null ? ValueInset : InputValueLeft;
        var right = row.Input is null || row.Output is null ? Width - ValueInset : InputValueRight;
        return new Rect(Origin.X + left, top, right - left, RowHeight - 4);
    }

    StepHit HitBelowHeader(Vector local)
    {
        var row = (int)((local.Y - HeaderHeight) / RowHeight);
        if (row < _rows.Count)
        {
            var hit = FieldOf(_rows[row], row).Contains(new Point(Origin.X + local.X, Origin.Y + local.Y));
            return hit && _rows[row].Parameter.Length > 0 ? new StepHit(StepPart.Value, Parameter: _rows[row].Parameter) : new StepHit(StepPart.Body);
        }
        var growth = _step.Growth;
        var centre = Width / 2;
        if (local.X >= centre - ButtonGap - ButtonWidth && local.X <= centre - ButtonGap && growth.Count < growth.Maximum)
        {
            return new StepHit(StepPart.Grow);
        }
        var shrinks = local.X >= centre + ButtonGap && local.X <= centre + ButtonGap + ButtonWidth && growth.Count > growth.Minimum;
        return new StepHit(shrinks ? StepPart.Shrink : StepPart.Body);
    }

    // The labels are made when the step is first drawn, so a step that is never on screen costs no text layout.
    List<Label> Labels()
    {
        if (_labels is not null)
        {
            return _labels;
        }
        _labels = [new Label(Text(Title, 12, EditorBrushes.Text, bold: true), new Point(TextInset, 5))];
        for (var i = 0; i < _rows.Count; i++)
        {
            AddRowLabels(_rows[i], HeaderHeight + (RowHeight * i) + 4);
        }
        if (HasGrowth)
        {
            AddGrowthLabels(HeaderHeight + (RowHeight * _rows.Count) + 2);
        }
        return _labels;
    }

    void AddRowLabels(Row row, double y)
    {
        if (row.Note.Length > 0)
        {
            _labels!.Add(new Label(Text(row.Note, 11, s_problem), new Point(TextInset, y)));
            return;
        }
        if (row.Input is not null)
        {
            _labels!.Add(new Label(Text(row.Input.Name, 11, EditorBrushes.Text), new Point(TextInset, y)));
        }
        if (row.Output is not null)
        {
            var text = Text(row.Output.Name, 11, EditorBrushes.Text);
            _labels!.Add(new Label(text, new Point(Width - TextInset - text.Width, y)));
        }
        if (row.Parameter.Length > 0)
        {
            AddValueLabel(row, y);
        }
    }

    void AddValueLabel(Row row, double y)
    {
        var shown = row.Value.Length > 0 ? row.Value : "empty";
        var brush = row.Value.Length > 0 ? EditorBrushes.Text : s_dim;
        var text = row.Input is null ? $"{row.Parameter}   {shown}" : shown;
        var left = row.Input is null ? ValueInset : InputValueLeft;
        _labels!.Add(new Label(Text(text, 11, brush), new Point(left + 4, y)));
    }

    void AddGrowthLabels(double top)
    {
        var centre = Width / 2;
        _labels!.Add(new Label(Text("+", 13, EditorBrushes.Text, bold: true), new Point(centre - ButtonGap - (ButtonWidth / 2) - 4, top)));
        _labels.Add(new Label(Text("−", 13, EditorBrushes.Text, bold: true), new Point(centre + ButtonGap + (ButtonWidth / 2) - 4, top)));
    }

    void DrawRows(DrawingContext context)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (row.Parameter.Length > 0)
            {
                context.DrawRectangle(s_field, null, new RoundedRect(FieldOf(row, i), 3));
            }
            DrawPin(context, row.Input, new PinRef(_step.Id, i, true));
            DrawPin(context, row.Output, new PinRef(_step.Id, i, false));
        }
        if (HasGrowth)
        {
            DrawGrowthButtons(context);
        }
    }

    void DrawPin(DrawingContext context, GraphPort? port, PinRef pin)
    {
        if (port is null)
        {
            return;
        }
        var centre = PinCenter(pin.IsInput, pin.Port);
        var brush = PinColors.Of(port);
        var filled = _wired.Contains(pin);
        if (port.Sort == GraphPortSort.Flow)
        {
            context.DrawGeometry(filled ? brush : null, new Pen(brush, 1.5).ToImmutable(), Triangle(centre));
            return;
        }
        context.DrawEllipse(filled ? brush : s_body, new Pen(brush, 1.5).ToImmutable(), centre, PinRadius, PinRadius);
    }

    void DrawGrowthButtons(DrawingContext context)
    {
        var top = Origin.Y + HeaderHeight + (RowHeight * _rows.Count) + 2;
        var centre = Origin.X + (Width / 2);
        var plus = new Rect(centre - ButtonGap - ButtonWidth, top, ButtonWidth, RowHeight - 4);
        var minus = new Rect(centre + ButtonGap, top, ButtonWidth, RowHeight - 4);
        context.DrawRectangle(s_field, s_border, new RoundedRect(plus, 3));
        context.DrawRectangle(s_field, s_border, new RoundedRect(minus, 3));
    }

    void DrawReroute(DrawingContext context, bool selected)
    {
        var brush = _step.Kind.EndsWith(FlowSuffix, StringComparison.Ordinal) ? PinColors.Flow : PinColors.Any;
        context.DrawEllipse(s_body, selected ? s_selected : new Pen(brush, 2).ToImmutable(), Bounds.Center, RerouteSize / 2, RerouteSize / 2);
    }

    static StreamGeometry Triangle(Point centre)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(centre.X - 4, centre.Y - 5), true);
        context.LineTo(new Point(centre.X + 5, centre.Y));
        context.LineTo(new Point(centre.X - 4, centre.Y + 5));
        context.EndFigure(true);
        return geometry;
    }
}
