using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>
/// Working on texture coordinates.
/// </summary>
/// <remarks>
/// Everything here goes one of two ways. Either it changes the rule a face gets its coordinates
/// from, which is cheap and survives the face changing shape; or it freezes the rule into
/// coordinates and edits those, which is the only way to say something the rule cannot.
/// <para>
/// A face is frozen the moment somebody drags one of its corners, and not before. That is the
/// whole of the automatic-to-manual story: nobody chooses it from a menu, it happens because they
/// did something a rule cannot express.
/// </para>
/// </remarks>
public static class MeshUvs
{
    extension(Mesh mesh)
    {
        /// <summary>
        /// Turns a face's rule into coordinates, so they can be edited one at a time. Already-frozen
        /// faces are left alone.
        /// </summary>
        public void FreezeUvs(IEnumerable<Face> which)
        {
            foreach (var face in which)
            {
                if (face.Uv.IsManual)
                {
                    continue;
                }

                face.Uv = face.Uv.CopyWith(manual: face.Uv.ForFace(mesh.PointsOf(face), mesh.NormalOf(face)));
            }
        }

        /// <summary>The coordinates a face currently has, whichever way it gets them.</summary>
        public List<Vector2> UvsOf(Face face) => face.Uv.ForFace(mesh.PointsOf(face), mesh.NormalOf(face));

        /// <summary>
        /// The box every one of these faces' coordinates fits in.
        /// </summary>
        /// <remarks>
        /// Null when there are none, which is the difference between "they are all at the origin"
        /// and "there is nothing to look at", and a UV view that framed the second as the first
        /// would show an empty square and no reason why.
        /// </remarks>
        public (Vector2 Min, Vector2 Max)? UvBoundsOf(IEnumerable<Face> which)
        {
            var minU = double.PositiveInfinity;
            var minV = double.PositiveInfinity;
            var maxU = double.NegativeInfinity;
            var maxV = double.NegativeInfinity;
            var found = false;

            foreach (var face in which)
            {
                foreach (var at in mesh.UvsOf(face))
                {
                    found = true;
                    minU = Math.Min(minU, at.X);
                    minV = Math.Min(minV, at.Y);
                    maxU = Math.Max(maxU, at.X);
                    maxV = Math.Max(maxV, at.Y);
                }
            }

            return found ? (new Vector2(minU, minV), new Vector2(maxU, maxV)) : null;
        }

        /// <summary>
        /// Scales coordinates about a point.
        /// </summary>
        /// <remarks>
        /// The point is in coordinate space, not on the face, which is what makes scaling a
        /// selection of several faces keep them where they are relative to each other rather than
        /// each shrinking into its own middle.
        /// </remarks>
        public void ScaleUvs(IEnumerable<Face> which, Vector2 by, Vector2? about = null)
        {
            var chosen = which.ToList();
            if ((about ?? mesh.MiddleOf(chosen)) is not { } centre)
            {
                return;
            }

            foreach (var face in chosen)
            {
                var drawn = face.Uv.Manual;
                if (drawn is null)
                {
                    // On the rule, so the scale is the rule's, and the offset has to move with it,
                    // or the face slides as it grows.
                    var was = face.Uv;
                    face.Uv = was.CopyWith(
                        scale: new Vector2(was.Scale.X * by.X, was.Scale.Y * by.Y),
                        offset: MeshUvs.ScaledAbout(was.Offset, centre, by));
                    continue;
                }

                face.Uv = face.Uv.CopyWith(manual: [.. drawn.Select(at => MeshUvs.ScaledAbout(at, centre, by))]);
            }
        }

        /// <summary>Turns coordinates about a point, in degrees.</summary>
        public void TurnUvs(IEnumerable<Face> which, double degrees, Vector2? about = null)
        {
            var chosen = which.ToList();
            if ((about ?? mesh.MiddleOf(chosen)) is not { } centre)
            {
                return;
            }

            var radians = degrees * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            foreach (var face in chosen)
            {
                var drawn = face.Uv.Manual;
                face.Uv = drawn is null
                    ? face.Uv.CopyWith(rotation: face.Uv.Rotation + degrees)
                    : face.Uv.CopyWith(manual: [.. drawn.Select(at => MeshUvs.TurnedAbout(at, centre, cos, sin))]);
            }
        }

        /// <summary>
        /// Projects several faces as though they were one flat surface.
        /// </summary>
        /// <remarks>
        /// From their average direction, so a texture runs across all of them without a seam. What
        /// somebody wants for a wall made of six quads and never wants for the six sides of a box,
        /// where it would smear the ones facing away.
        /// </remarks>
        public void ProjectPlanar(IEnumerable<Face> which)
        {
            var chosen = which.ToList();
            if (chosen.Count == 0)
            {
                return;
            }

            var normal = Vector3.Zero();
            foreach (var face in chosen)
            {
                normal.Add(mesh.NormalOf(face));
            }

            if (normal.Length2 < 1e-12)
            {
                return;
            }

            var axes = FaceUv.AxesFor(normal.Normalized());
            foreach (var face in chosen)
            {
                face.Uv = face.Uv.CopyWith(manual: MeshUvs.Flattened(mesh.PointsOf(face), axes));
            }
        }

        /// <summary>
        /// Projects each face from whichever way it happens to point.
        /// </summary>
        /// <remarks>
        /// Which is what the rule already does, so this is the rule made explicit: the faces are
        /// frozen where they stand. Useful before hand-editing, and as the way back from a planar
        /// projection that smeared something.
        /// </remarks>
        public void ProjectBox(IEnumerable<Face> which)
        {
            foreach (var face in which)
            {
                var axes = FaceUv.AxesFor(mesh.NormalOf(face));
                face.Uv = face.Uv.CopyWith(manual: MeshUvs.Flattened(mesh.PointsOf(face), axes));
            }
        }

        /// <summary>
        /// Fits the coordinates of these faces into the nought-to-one square, together, so their
        /// arrangement is kept and only their size changes.
        /// </summary>
        public void FitUvs(IEnumerable<Face> which, bool keepShape = true)
        {
            var chosen = which.ToList();
            if (mesh.UvBoundsOf(chosen) is not { } box)
            {
                return;
            }

            var spanU = box.Max.X - box.Min.X;
            var spanV = box.Max.Y - box.Min.Y;
            if (spanU < 1e-9)
            {
                spanU = 1;
            }

            if (spanV < 1e-9)
            {
                spanV = 1;
            }

            if (keepShape)
            {
                var most = Math.Max(spanU, spanV);
                spanU = most;
                spanV = most;
            }

            mesh.FreezeUvs(chosen);
            foreach (var face in chosen)
            {
                if (face.Uv.Manual is not { } drawn)
                {
                    continue;
                }

                face.Uv = face.Uv.CopyWith(
                    manual: [.. drawn.Select(at => new Vector2((at.X - box.Min.X) / spanU, (at.Y - box.Min.Y) / spanV))]);
            }
        }

        /// <summary>Mirrors coordinates.</summary>
        public void FlipUvs(IEnumerable<Face> which, bool u = false, bool v = false)
        {
            if (!u && !v)
            {
                return;
            }

            foreach (var face in which)
            {
                var drawn = face.Uv.Manual;
                if (drawn is null)
                {
                    face.Uv = face.Uv.CopyWith(
                        flipU: u ? !face.Uv.FlipU : face.Uv.FlipU,
                        flipV: v ? !face.Uv.FlipV : face.Uv.FlipV);
                    continue;
                }

                var centre = mesh.MiddleOf([face]) ?? Vector2.Zero();
                face.Uv = face.Uv.CopyWith(manual: [.. drawn.Select(at => MeshUvs.MirroredAbout(at, centre, u, v))]);
            }
        }

        /// <summary>The middle of what these faces cover, in coordinate space.</summary>
        private Vector2? MiddleOf(List<Face> which) =>
            mesh.UvBoundsOf(which) is { } box ? (box.Min + box.Max) * 0.5 : null;
    }

    /// <summary>
    /// Back to the rule, throwing away whatever was drawn.
    /// </summary>
    /// <remarks>
    /// The settings underneath are kept rather than reset: somebody who tried editing by hand
    /// and changed their mind wants the offset and scale they had, not the defaults.
    /// </remarks>
    public static void ReleaseUvs(IEnumerable<Face> which)
    {
        foreach (var face in which)
        {
            face.Uv = face.Uv.CopyWith(clearManual: true);
        }
    }

    /// <summary>
    /// Moves coordinates.
    /// </summary>
    /// <remarks>
    /// A face still on its rule has the move folded into the rule's offset, which keeps it
    /// automatic. One that has been frozen has its corners moved. Same gesture either way, and
    /// the difference only shows later, when the face changes shape.
    /// </remarks>
    public static void NudgeUvs(IEnumerable<Face> which, Vector2 by)
    {
        foreach (var face in which)
        {
            var drawn = face.Uv.Manual;
            face.Uv = drawn is null
                ? face.Uv.CopyWith(offset: face.Uv.Offset + by)
                : face.Uv.CopyWith(manual: [.. drawn.Select(at => at + by)]);
        }
    }

    private static Vector2 ScaledAbout(Vector2 at, Vector2 centre, Vector2 by) =>
        new(centre.X + ((at.X - centre.X) * by.X), centre.Y + ((at.Y - centre.Y) * by.Y));

    private static Vector2 TurnedAbout(Vector2 at, Vector2 centre, double cos, double sin) => new(
        centre.X + ((at.X - centre.X) * cos) - ((at.Y - centre.Y) * sin),
        centre.Y + ((at.X - centre.X) * sin) + ((at.Y - centre.Y) * cos));

    private static Vector2 MirroredAbout(Vector2 at, Vector2 centre, bool u, bool v) =>
        new(u ? (2 * centre.X) - at.X : at.X, v ? (2 * centre.Y) - at.Y : at.Y);

    private static List<Vector2> Flattened(List<Vector3> points, (Vector3 U, Vector3 V) axes) =>
        [.. points.Select(at => new Vector2(at.Dot(axes.U), at.Dot(axes.V)))];
}
