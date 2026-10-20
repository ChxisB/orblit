using System.Diagnostics.CodeAnalysis;
using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>
/// The shapes somebody starts from. Parametric on purpose: a box is a width, a height and a depth
/// until somebody pulls a face off it, and until then changing its size should change its size
/// rather than move eight corners.
/// </summary>
public enum ShapeKind
{
    Cube,
    Sprite,
    Prism,
    Plane,
    Sphere,
    Cylinder,
    Cone,
    Pipe,
    Torus,
    Arch,
    Door,
    Stairs,
}

public static class ShapeKinds
{
    extension(ShapeKind kind)
    {
        /// <summary>The name a person reads, which is the member's own name.</summary>
        public string Label => kind.ToString();
    }

    /// <summary>The kind whose name is <paramref name="name"/>, as Dart's <c>name</c> spells it, or null when none is.</summary>
    public static ShapeKind? Named(object? name) =>
        Enum.GetValues<ShapeKind>().Cast<ShapeKind?>().FirstOrDefault(kind => name is string text && EnumNames.Of(kind!.Value) == text);
}

/// <summary>
/// What a shape is made of, before it is made. One type for every kind and not one per kind: the
/// fields that mean nothing to a plane cost a few bytes, and the alternative is a sealed hierarchy
/// that every caller has to switch over to read a width. Change a field with <c>with</c>.
/// </summary>
public sealed record Shape
{
    private static readonly int[][] IcosahedronFaces =
    [
        [0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11],
        [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
        [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9],
        [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1],
    ];

    public required ShapeKind Kind { get; init; }

    public double Width { get; init; } = 1;

    public double Height { get; init; } = 1;

    public double Depth { get; init; } = 1;

    /// <summary>How many faces go round a cylinder, a cone, a pipe or an arch.</summary>
    public int Sides { get; init; } = 6;

    /// <summary>How many rows a torus has round its main circle.</summary>
    public int Rings { get; init; } = 16;

    /// <summary>How many columns a torus has round its tube.</summary>
    public int Columns { get; init; } = 24;

    /// <summary>How many steps a staircase has, when it is counted and not measured.</summary>
    public int Steps { get; init; } = 10;

    /// <summary>
    /// How many times a sphere is divided. An icosphere and not a globe of latitude lines: the faces
    /// are all about the same size, which is what makes it deform evenly and shade without pinching
    /// at the poles.
    /// </summary>
    public int Subdivisions { get; init; } = 3;

    /// <summary>How many extra cuts a plane has across it.</summary>
    public int WidthCuts { get; init; } = 1;

    /// <summary>How many extra cuts a plane, a cylinder or a pipe has up it.</summary>
    public int HeightCuts { get; init; }

    /// <summary>How thick the wall of a pipe, an arch or a door is.</summary>
    public double Thickness { get; init; } = 0.25;

    /// <summary>How fat a torus's tube is.</summary>
    public double TubeRadius { get; init; } = 0.1;

    /// <summary>
    /// How far round an arch or a torus goes, in degrees. A hundred and eighty is a semicircle.
    /// </summary>
    public double Circumference { get; init; } = 360;

    /// <summary>How tall the piece over a door's opening is.</summary>
    public double PedimentHeight { get; init; } = 0.5;

    /// <summary>How wide a door's two uprights are.</summary>
    public double SideWidth { get; init; } = 0.75;

    /// <summary>Whether a cylinder, a pipe or an arch has ends on it.</summary>
    public bool Capped { get; init; } = true;

    /// <summary>Whether the curved parts shade round and not faceted.</summary>
    public bool Smooth { get; init; } = true;

    /// <summary>
    /// Whether a staircase is given a number of steps or a step height. A number of steps fits a
    /// flight to a gap. A height makes every step the one a building code allows, and the flight is
    /// however long it needs to be.
    /// </summary>
    public bool ByCount { get; init; } = true;

    public double StepHeight { get; init; } = 0.2;

    /// <summary>
    /// A shape of this kind, with the defaults that kind wants. One set of defaults for twelve
    /// primitives would be twelve compromises: an arch is a semicircle and a torus is a whole one.
    /// </summary>
    public static Shape Of(ShapeKind kind) => kind switch
    {
        ShapeKind.Arch => new Shape { Kind = kind, Circumference = 180, Thickness = 0.1 },
        ShapeKind.Torus => new Shape { Kind = kind, Circumference = 360 },
        ShapeKind.Cone => new Shape { Kind = kind, Sides = 6 },
        ShapeKind.Sphere => new Shape { Kind = kind, Subdivisions = 3 },
        ShapeKind.Pipe => new Shape { Kind = kind, Thickness = 0.25 },
        ShapeKind.Plane => new Shape { Kind = kind, WidthCuts = 1, HeightCuts = 1 },
        _ => new Shape { Kind = kind },
    };

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["kind"] = EnumNames.Of(Kind),
        ["width"] = Width,
        ["height"] = Height,
        ["depth"] = Depth,
        ["sides"] = (long)Sides,
        ["rings"] = (long)Rings,
        ["columns"] = (long)Columns,
        ["steps"] = (long)Steps,
        ["subdivisions"] = (long)Subdivisions,
        ["widthCuts"] = (long)WidthCuts,
        ["heightCuts"] = (long)HeightCuts,
        ["thickness"] = Thickness,
        ["tubeRadius"] = TubeRadius,
        ["circumference"] = Circumference,
        ["pedimentHeight"] = PedimentHeight,
        ["sideWidth"] = SideWidth,
        ["capped"] = Capped,
        ["smooth"] = Smooth,
        ["byCount"] = ByCount,
        ["stepHeight"] = StepHeight,
    };

    public static Shape? FromJson(object? value)
    {
        if (JsonValues.Map(value) is not { } map || ShapeKinds.Named(map.GetValueOrDefault("kind")) is not { } kind)
        {
            return null;
        }

        double Number(string key, double fallback) => JsonValues.Num(map.GetValueOrDefault(key)) ?? fallback;
        int Count(string key, int fallback) => JsonValues.Whole(map.GetValueOrDefault(key)) ?? fallback;
        bool Flag(string key) => map.GetValueOrDefault(key) is not false;

        return new Shape
        {
            Kind = kind,
            Width = Number("width", 1),
            Height = Number("height", 1),
            Depth = Number("depth", 1),
            Sides = Count("sides", 6),
            Rings = Count("rings", 16),
            Columns = Count("columns", 24),
            Steps = Count("steps", 10),
            Subdivisions = Count("subdivisions", 3),
            WidthCuts = Count("widthCuts", 1),
            HeightCuts = Count("heightCuts", 0),
            Thickness = Number("thickness", 0.25),
            TubeRadius = Number("tubeRadius", 0.1),
            Circumference = Number("circumference", 360),
            PedimentHeight = Number("pedimentHeight", 0.5),
            SideWidth = Number("sideWidth", 0.75),
            Capped = Flag("capped"),
            Smooth = Flag("smooth"),
            ByCount = Flag("byCount"),
            StepHeight = Number("stepHeight", 0.2),
        };
    }

    /// <summary>
    /// The geometry this shape describes. Centred on the origin and standing on the ground, which is
    /// where somebody putting a box in a scene expects it: a shape whose middle is its centre sinks
    /// half into the floor the moment it is placed.
    /// </summary>
    public Mesh Build()
    {
        // Clamped and not trusted. A cylinder with two sides is a flat sheet and one with two
        // thousand is a frame nobody asked to lose; both are what a dragged slider produces on the
        // way to the number somebody wanted.
        var around = Math.Clamp(Sides, 3, 256);
        var up = Math.Clamp(Rings, 3, 64);

        // Counted, or worked out from how tall each step is. Either way at least one, because a
        // flight of no steps is a gap.
        var flights = ByCount
            ? Math.Clamp(Steps, 1, 256)
            : (int)Math.Clamp(Doubles.Round(Height / (StepHeight <= 0.01 ? 0.01 : StepHeight)), 1L, 256L);

        var mesh = BuildKind(around, up, flights);

        // Stood on the ground, once, here. Leaving each of the twelve builders to remember that is
        // leaving eleven of them to forget.
        var floor = mesh.Bounds.Min.Y;
        if (Math.Abs(floor) > 1e-12)
        {
            mesh.Transform(Matrix4.TranslationValues(0, -floor, 0));
        }

        return mesh;
    }

    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private Mesh BuildKind(int around, int up, int flights) => Kind switch
    {
        ShapeKind.Cube => MakeCube(),

        // A plane with all three sides at one unit, which is what a sprite is: somewhere to put a picture.
        ShapeKind.Sprite => new Shape { Kind = ShapeKind.Plane, Width = 1, Depth = 1, WidthCuts = 0, HeightCuts = 0 }.MakePlane(0, 0),
        ShapeKind.Prism => MakePrism(),
        ShapeKind.Plane => MakePlane(Math.Clamp(WidthCuts, 0, 128), Math.Clamp(HeightCuts, 0, 128)),
        ShapeKind.Sphere => MakeSphere(Math.Clamp(Subdivisions, 1, 5)),
        ShapeKind.Cylinder => MakeCylinder(around, Math.Clamp(HeightCuts, 0, 64)),
        ShapeKind.Cone => MakeCone(around),
        ShapeKind.Pipe => MakePipe(around, Math.Clamp(HeightCuts, 0, 31)),
        ShapeKind.Torus => MakeTorus(up, Math.Clamp(Columns, 3, 64)),
        ShapeKind.Arch => MakeArch(Math.Clamp(Sides, 2, 200)),
        ShapeKind.Door => MakeDoor(),
        ShapeKind.Stairs => MakeStairs(flights),
        _ => throw new InvalidOperationException($"{Kind} is not a shape kind."),
    };

    /// <summary>A grid of quads, so there are edges to pull on later.</summary>
    private Mesh MakePlane(int across, int along)
    {
        var columns = across + 1;
        var rows = along + 1;
        var mesh = new Mesh();

        for (var z = 0; z <= rows; z++)
        {
            for (var x = 0; x <= columns; x++)
            {
                mesh.AddVertex(new Vector3(((double)x / columns - 0.5) * Width, 0, ((double)z / rows - 0.5) * Depth));
            }
        }

        int At(int x, int z) => (z * (columns + 1)) + x;
        for (var z = 0; z < rows; z++)
        {
            for (var x = 0; x < columns; x++)
            {
                // Wound so it faces up, which is the only way a floor is any use.
                mesh.AddFace([At(x, z), At(x, z + 1), At(x + 1, z + 1), At(x + 1, z)]);
            }
        }

        return mesh;
    }

    private Mesh MakeCube()
    {
        var x = Width / 2;
        var z = Depth / 2;
        var mesh = new Mesh(
            positions:
            [
                new Vector3(-x, 0, -z),
                new Vector3(x, 0, -z),
                new Vector3(x, 0, z),
                new Vector3(-x, 0, z),
                new Vector3(-x, Height, -z),
                new Vector3(x, Height, -z),
                new Vector3(x, Height, z),
                new Vector3(-x, Height, z),
            ]);

        // Every one wound counter-clockwise seen from outside, which is what decides whether a
        // face is there or whether the shape has a hole in it.
        mesh.AddFace([0, 1, 2, 3]); // bottom, facing down
        mesh.AddFace([4, 7, 6, 5]); // top
        mesh.AddFace([0, 4, 5, 1]); // back
        mesh.AddFace([3, 2, 6, 7]); // front
        mesh.AddFace([1, 5, 6, 2]); // right
        mesh.AddFace([0, 3, 7, 4]); // left
        return mesh;
    }

    /// <summary>A box with a roof: a triangular prism lying along Z.</summary>
    private Mesh MakePrism()
    {
        var x = Width / 2;
        var z = Depth / 2;
        var mesh = new Mesh(
            positions:
            [
                new Vector3(-x, 0, -z),
                new Vector3(x, 0, -z),
                new Vector3(x, 0, z),
                new Vector3(-x, 0, z),
                new Vector3(0, Height, -z),
                new Vector3(0, Height, z),
            ]);

        mesh.AddFace([0, 1, 2, 3]); // bottom
        mesh.AddFace([0, 3, 5, 4]); // left slope
        mesh.AddFace([1, 4, 5, 2]); // right slope
        mesh.AddFace([0, 4, 1]); // the gable at the back
        mesh.AddFace([3, 2, 5]); // and at the front
        return mesh;
    }

    /// <summary>
    /// A ball made by dividing an icosahedron. Not latitude and longitude: those crowd every face
    /// into the poles, where a texture pinches and a deformation bunches.
    /// </summary>
    private Mesh MakeSphere(int times)
    {
        // The twelve corners of an icosahedron, from the golden ratio.
        const double t = 1.618033988749895;
        var mesh = new Mesh(
            positions:
            [
                new Vector3(-1, t, 0),
                new Vector3(1, t, 0),
                new Vector3(-1, -t, 0),
                new Vector3(1, -t, 0),
                new Vector3(0, -1, t),
                new Vector3(0, 1, t),
                new Vector3(0, -1, -t),
                new Vector3(0, 1, -t),
                new Vector3(t, 0, -1),
                new Vector3(t, 0, 1),
                new Vector3(-t, 0, -1),
                new Vector3(-t, 0, 1),
            ]);

        foreach (var face in IcosahedronFaces)
        {
            mesh.AddFace([.. face], smooth: Smooth);
        }

        for (var pass = 1; pass < times; pass++)
        {
            Subdivide(mesh);
        }

        // Onto the sphere, then scaled to the box asked for and stood on the ground like every
        // other shape.
        foreach (var at in mesh.Positions)
        {
            at.Normalize();
            at.X *= Width / 2;
            at.Y *= Height / 2;
            at.Z *= Depth / 2;
            at.Y += Height / 2;
        }

        return mesh;
    }

    /// <summary>Splits every triangle into four and pushes the new corners out onto the sphere.</summary>
    private void Subdivide(Mesh mesh)
    {
        var split = new Dictionary<(int, int), int>();
        var grown = new Mesh(positions: [.. mesh.Positions.Select(at => at.Clone())]);

        // F5: a lookup that adds on a miss, as a map's putIfAbsent does.
        int Between(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (!split.TryGetValue(key, out var found))
            {
                found = grown.AddVertex((grown.Positions[a] + grown.Positions[b]).Normalized());
                split[key] = found;
            }

            return found;
        }

        foreach (var face in mesh.Faces)
        {
            var a = face.Vertices[0];
            var b = face.Vertices[1];
            var c = face.Vertices[2];
            var ab = Between(a, b);
            var bc = Between(b, c);
            var ca = Between(c, a);
            grown.AddFace([a, ab, ca], smooth: Smooth);
            grown.AddFace([b, bc, ab], smooth: Smooth);
            grown.AddFace([c, ca, bc], smooth: Smooth);
            grown.AddFace([ab, bc, ca], smooth: Smooth);
        }

        mesh.Positions.Clear();
        mesh.Positions.AddRange(grown.Positions);
        mesh.Faces.Clear();
        mesh.Faces.AddRange(grown.Faces);
    }

    private Mesh MakeCylinder(int around, int cuts)
    {
        var mesh = new Mesh();
        var radiusX = Width / 2;
        var radiusZ = Depth / 2;
        var levels = cuts + 1;

        for (var i = 0; i < around; i++)
        {
            var angle = (double)i / around * Math.PI * 2;
            var x = Math.Cos(angle) * radiusX;
            var z = Math.Sin(angle) * radiusZ;
            for (var level = 0; level <= levels; level++)
            {
                mesh.AddVertex(new Vector3(x, Height * level / levels, z));
            }
        }

        int At(int segment, int level) => (segment % around * (levels + 1)) + level;

        for (var i = 0; i < around; i++)
        {
            for (var level = 0; level < levels; level++)
            {
                // Up the near edge, along the top, down the far one: the other order winds the
                // side inwards and the cylinder is invisible from outside.
                mesh.AddFace([At(i, level), At(i, level + 1), At(i + 1, level + 1), At(i + 1, level)], smooth: Smooth);
            }
        }

        if (Capped)
        {
            // Round one way for the bottom and the other for the top. Going round in increasing
            // angle is clockwise seen from above and so faces down, right for the bottom and
            // exactly wrong for the top.
            mesh.AddFace([.. Enumerable.Range(0, around).Select(i => At(i, 0))]);
            mesh.AddFace([.. Enumerable.Range(0, around).Reverse().Select(i => At(i, levels))]);
        }

        return mesh;
    }

    private Mesh MakeCone(int around)
    {
        var mesh = new Mesh();
        var radiusX = Width / 2;
        var radiusZ = Depth / 2;

        var tip = mesh.AddVertex(new Vector3(0, Height, 0));
        for (var i = 0; i < around; i++)
        {
            var angle = (double)i / around * Math.PI * 2;
            mesh.AddVertex(new Vector3(Math.Cos(angle) * radiusX, 0, Math.Sin(angle) * radiusZ));
        }

        for (var i = 0; i < around; i++)
        {
            var here = 1 + i;
            var next = 1 + ((i + 1) % around);
            mesh.AddFace([here, tip, next], smooth: Smooth);
        }

        if (Capped)
        {
            // Forward, in increasing angle, which is clockwise from above and so faces down, which
            // is what the underside of a cone wants.
            mesh.AddFace([.. Enumerable.Range(1, around)]);
        }

        return mesh;
    }

    /// <summary>A cylinder with the middle taken out.</summary>
    private Mesh MakePipe(int around, int cuts)
    {
        var mesh = new Mesh();
        var outerX = Width / 2;
        var outerZ = Depth / 2;
        var wall = Doubles.Clamp(Thickness, 0.01, Math.Min(outerX, outerZ) * 0.99);
        var levels = cuts + 1;

        for (var i = 0; i < around; i++)
        {
            var angle = (double)i / around * Math.PI * 2;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            for (var level = 0; level <= levels; level++)
            {
                var y = Height * level / levels;
                mesh.AddVertex(new Vector3(cos * outerX, y, sin * outerZ));
                mesh.AddVertex(new Vector3(cos * (outerX - wall), y, sin * (outerZ - wall)));
            }
        }

        var perSegment = (levels + 1) * 2;
        int Outer(int i, int level) => (i % around * perSegment) + (level * 2);
        int Inner(int i, int level) => Outer(i, level) + 1;

        for (var i = 0; i < around; i++)
        {
            for (var level = 0; level < levels; level++)
            {
                // Outside, and the inside wound the other way so it faces inwards.
                mesh.AddFace([Outer(i, level), Outer(i, level + 1), Outer(i + 1, level + 1), Outer(i + 1, level)], smooth: Smooth);
                mesh.AddFace([Inner(i, level), Inner(i + 1, level), Inner(i + 1, level + 1), Inner(i, level + 1)], smooth: Smooth);
            }

            if (Capped)
            {
                mesh.AddFace([Outer(i, 0), Inner(i, 0), Inner(i + 1, 0), Outer(i + 1, 0)]);
                mesh.AddFace([Outer(i, levels), Outer(i + 1, levels), Inner(i + 1, levels), Inner(i, levels)]);
            }
        }

        return mesh;
    }

    /// <summary>A ring with a round section.</summary>
    private Mesh MakeTorus(int rows, int columns)
    {
        var mesh = new Mesh();
        var ringX = (Width / 2) - TubeRadius;
        var ringZ = (Depth / 2) - TubeRadius;
        var tube = Doubles.Clamp(TubeRadius, 0.001, Math.Min(Width, Depth) / 2);

        var sweep = Doubles.Clamp(Circumference, 1.0, 360.0) / 360 * Math.PI * 2;
        var closed = Circumference >= 359.999;
        var steps = closed ? rows : rows + 1;

        for (var row = 0; row < steps; row++)
        {
            var around = (double)row / rows * sweep;
            var cos = Math.Cos(around);
            var sin = Math.Sin(around);
            for (var column = 0; column < columns; column++)
            {
                var through = (double)column / columns * Math.PI * 2;
                var outward = Math.Cos(through) * tube;
                mesh.AddVertex(new Vector3(cos * (ringX + outward), (Math.Sin(through) * tube) + tube, sin * (ringZ + outward)));
            }
        }

        int At(int row, int column) => (row % steps * columns) + (column % columns);
        for (var row = 0; row < (closed ? steps : steps - 1); row++)
        {
            for (var column = 0; column < columns; column++)
            {
                mesh.AddFace([At(row, column), At(row, column + 1), At(row + 1, column + 1), At(row + 1, column)], smooth: Smooth);
            }
        }

        return mesh;
    }

    /// <summary>A doorway: two uprights and a piece across the top.</summary>
    private Mesh MakeDoor()
    {
        var side = Doubles.Clamp(SideWidth, 0.01, (Width / 2) - 0.01);
        var pediment = Doubles.Clamp(PedimentHeight, 0.01, Height - 0.01);
        var mesh = new Mesh();

        // Left upright, right upright, and the lintel over the gap.
        (double W, double H, double X, double Y)[] parts =
        [
            (side, Height - pediment, -(Width - side) / 2, 0.0),
            (side, Height - pediment, (Width - side) / 2, 0.0),
            (Width, pediment, 0.0, Height - pediment),
        ];
        foreach (var part in parts)
        {
            var piece = new Shape { Kind = ShapeKind.Cube, Width = part.W, Height = part.H, Depth = Depth }.Build();
            piece.Transform(Matrix4.TranslationValues(part.X, part.Y, 0));
            mesh.Merge(piece);
        }

        return mesh;
    }

    private Mesh MakeStairs(int flights)
    {
        var mesh = new Mesh();
        var rise = Height / flights;
        var run = Depth / flights;

        // Each step is a box, and the boxes share nothing: a staircase somebody is about to pull a
        // step off is easier to work with than one welded solid, and welding is an operation they
        // can ask for.
        for (var i = 0; i < flights; i++)
        {
            var z0 = (-Depth / 2) + (i * run);
            var step = new Shape { Kind = ShapeKind.Cube, Width = Width, Height = rise * (i + 1), Depth = run }.Build();
            step.Transform(Matrix4.TranslationValues(0, 0, z0 + (run / 2)));
            mesh.Merge(step);
        }

        return mesh;
    }

    /// <summary>A curved wall: an arc swept through a thickness, with a flat end at each side.</summary>
    private Mesh MakeArch(int segments)
    {
        var mesh = new Mesh();
        var outerX = Width / 2;
        var wall = Doubles.Clamp(Thickness, 0.01, Math.Min(outerX, Height) * 0.99);
        var z = Depth / 2;
        var sweep = Doubles.Clamp(Circumference, 1.0, 360.0) / 360 * Math.PI * 2;
        var closed = Circumference >= 359.999;

        // Measured from one side round to the other, so a hundred and eighty degrees is a
        // semicircle standing on its ends.
        var start = Math.PI - ((sweep - Math.PI) / 2);

        for (var i = 0; i <= segments; i++)
        {
            var angle = start - ((double)i / segments * sweep);
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            foreach (var side in new[] { -z, z })
            {
                mesh.AddVertex(new Vector3(cos * outerX, sin * Height, side));
                mesh.AddVertex(new Vector3(cos * (outerX - wall), sin * (Height - wall), side));
            }
        }

        // Four points a step: outer-back, inner-back, outer-front, inner-front.
        static int At(int step, int which) => (step * 4) + which;

        for (var a = 0; a < segments; a++)
        {
            var b = a + 1;

            // The outside of the curve, and the inside wound the other way.
            mesh.AddFace([At(a, 0), At(b, 0), At(b, 2), At(a, 2)], smooth: Smooth);
            mesh.AddFace([At(a, 1), At(a, 3), At(b, 3), At(b, 1)], smooth: Smooth);

            // And the two flat sides.
            mesh.AddFace([At(a, 0), At(a, 1), At(b, 1), At(b, 0)]);
            mesh.AddFace([At(a, 2), At(b, 2), At(b, 3), At(a, 3)]);
        }

        // The two cut ends, unless it goes all the way round and meets itself.
        if (Capped && !closed)
        {
            mesh.AddFace([At(0, 0), At(0, 2), At(0, 3), At(0, 1)]);
            mesh.AddFace([At(segments, 0), At(segments, 1), At(segments, 3), At(segments, 2)]);
        }

        return mesh;
    }
}
