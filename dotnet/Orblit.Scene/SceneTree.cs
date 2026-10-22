using System.Collections.Frozen;

namespace Orblit.Scene;

/// <summary>
/// Makes the parent links into a tree.
/// </summary>
/// <remarks>
/// Not exported: what calls it is the reading of a scene, a prefab, and the opening of instances,
/// and each of those says what it repaired.
/// </remarks>
internal static class SceneTree
{
    /// <summary>
    /// Two things can be wrong with the links, and both come from files people hand edit or merge. A
    /// parent that is not in the file would leave its child unreachable, so the child becomes a root
    /// instead of disappearing. And a loop would hang the outliner while it drew, which is the worst
    /// possible place to find one, so it is cut.
    /// <para>
    /// A parent inside an instance is an <see cref="EntityPath"/>, and an instance that is not open
    /// has no parts to find: a link into one is left alone as long as the instance is there, because
    /// opening it is what will make it true. In a <paramref name="folded"/> document that is every
    /// instance; after opening, it is the <paramref name="closed"/> ones, whose prefab could not be
    /// read. Anywhere else, a link to a part that is not there falls back to the nearest thing it
    /// was inside that is, a sign hung on a lamp that its prefab no longer has stays on the street
    /// it was on.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<SceneEntity> Rooted(
        IReadOnlyList<SceneEntity> entities,
        List<string> problems,
        bool folded = false,
        IReadOnlySet<string>? closed = null)
    {
        var links = new Links(entities, problems);
        links.Reattach(folded, closed ?? FrozenSet<string>.Empty);
        links.CutLoops();
        return links.Moved.Count == 0 ? entities : links.Applied();
    }

    private sealed class Links
    {
        private readonly IReadOnlyList<SceneEntity> _entities;
        private readonly List<string> _problems;
        private readonly HashSet<string> _present;
        private readonly Dictionary<string, string?> _parents = [];

        public Links(IReadOnlyList<SceneEntity> entities, List<string> problems)
        {
            _entities = entities;
            _problems = problems;
            _present = [.. entities.Select(entity => entity.Id)];
            foreach (var entity in entities)
            {
                _parents[entity.Id] = entity.Parent;
            }
        }

        public HashSet<string> Moved { get; } = [];

        public void Reattach(bool folded, IReadOnlySet<string> closed)
        {
            foreach (var entity in _entities)
            {
                if (entity.Parent is { } parent && !_present.Contains(parent))
                {
                    Reattach(entity, parent, folded, closed);
                }
            }
        }

        public void CutLoops()
        {
            foreach (var entity in _entities)
            {
                var walked = new HashSet<string> { entity.Id };
                var current = _parents[entity.Id];
                while (current is not null)
                {
                    if (!walked.Add(current))
                    {
                        _problems.Add($"\"{entity.Name}\" was inside itself, and is now at the top level.");
                        Moved.Add(entity.Id);
                        _parents[entity.Id] = null;
                        break;
                    }

                    current = _parents.GetValueOrDefault(current);
                }
            }
        }

        public List<SceneEntity> Applied()
        {
            var next = new List<SceneEntity>(_entities.Count);
            foreach (var entity in _entities)
            {
                if (!Moved.Contains(entity.Id))
                {
                    next.Add(entity);
                }
                else if (_parents[entity.Id] is { } parent)
                {
                    next.Add(entity.CopyWith(parent: parent));
                }
                else
                {
                    next.Add(entity.CopyWith(clearParent: true));
                }
            }

            return next;
        }

        private void Reattach(SceneEntity entity, string parent, bool folded, IReadOnlySet<string> closed)
        {
            var fallback = EntityPath.Enclosing(parent).Where(_present.Contains).ToList();
            if (fallback.Count > 0 && (folded || closed.Contains(fallback[0])))
            {
                return;
            }

            if (fallback.Count > 0)
            {
                _problems.Add(
                    $"\"{entity.Name}\" was on something inside \"{fallback[0]}\" that is no longer there, "
                    + $"and is now on \"{fallback[0]}\" itself.");
                _parents[entity.Id] = fallback[0];
            }
            else
            {
                _problems.Add($"\"{entity.Name}\" belonged to something that is not in this file, and is now at the top level.");
                _parents[entity.Id] = null;
            }

            Moved.Add(entity.Id);
        }
    }
}
