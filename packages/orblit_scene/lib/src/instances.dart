import 'component.dart';
import 'components/data.dart';
import 'diff.dart';
import 'document.dart';
import 'entity.dart';
import 'path.dart';
import 'prefab.dart';

/// The prefab at [asset], or null when there is none to be had.
///
/// A function rather than a folder, because what a prefab *is* at a given
/// moment is the caller's to say: an editor answers from what it has open,
/// which may be newer than the file; a game answers from what it shipped; a
/// test answers from a map.
typedef PrefabSource = PrefabDocument? Function(String asset);

/// Something asked of a prefab that cannot be done.
class PrefabException implements Exception {
  const PrefabException(this.message);

  final String message;

  @override
  String toString() => message;
}

/// What making a prefab, or applying an instance to one, produces: the prefab
/// to write, and the scene with the instance linked to it.
class PrefabEdit {
  const PrefabEdit({
    required this.prefab,
    required this.document,
    this.renamed = const {},
    this.problems = const [],
  });

  final PrefabDocument prefab;

  final SceneDocument document;

  /// Old id to new, for everything that became a part and so took a path.
  /// Whatever holds ids — a selection — follows these.
  final Map<String, String> renamed;

  final List<String> problems;
}

/// [document] with its instances opened: every part of every prefab put in,
/// under ids that are [EntityPath]s into the instance it belongs to.
///
/// What a document is read into before anything uses it. The parts are then
/// ordinary entities — drawn, selected, edited, animated by path — and what an
/// instance changes about its prefab is simply how its parts differ from the
/// prefab's, worked out again by [foldInstances] when it is saved.
///
/// Opens prefabs inside prefabs, all the way down, and copies stamped into a
/// scene before instances were links are relinked here, keeping everything
/// that was changed about them. An instance whose prefab cannot be read, or
/// which contains itself, stays folded: a link that still says everything it
/// said. Each of those is a problem in the result rather than a failure.
///
/// [only] limits it to those instances, by id.
SceneLoad expandInstances(
  SceneDocument document,
  PrefabSource source, {
  Iterable<String>? only,
}) {
  final problems = <String>[];
  final opened = _Opener(
    source,
    problems,
  ).openAll(document, only: only?.toSet());
  return SceneLoad(document: opened, problems: problems);
}

/// [document] with its open instances folded back into links, as it is saved.
///
/// Each one becomes the entity it was — name, place in the tree, visibility —
/// carrying a [PrefabComponent] whose overrides are the difference between
/// the prefab and the parts as they stand now, and the parts themselves are
/// left out. Something hung on a part from outside the instance is not part
/// of it: it belongs to this document, is written here, and keeps its link
/// into the instance by path.
///
/// An instance whose prefab [source] cannot give is left open. Folding it
/// against nothing would say it had removed everything, and writing it as it
/// stands keeps it — [expandInstances] recognises an instance already open and
/// leaves its parts alone.
///
/// A part stays inside the instance it belongs to. One whose parent has been
/// set to something outside it cannot be said as a change to the instance,
/// and is folded back under the instance's root: moving parts out is for
/// [unpackInstance] to do first.
///
/// [only] limits it to those instances, by id.
SceneDocument foldInstances(
  SceneDocument document,
  PrefabSource source, {
  Iterable<String>? only,
}) {
  final wanted = only?.toSet();
  final opener = _Opener(source, []);
  final stubs = <String, SceneEntity>{};

  for (final entity in document.entities) {
    final link = _linkOf(entity);
    if (link == null || link.state != PrefabState.open) continue;
    if (EntityPath.isPart(entity.id)) continue;
    if (wanted != null && !wanted.contains(entity.id)) continue;
    final base = opener.open(link.asset!);
    if (base == null) continue;
    stubs[entity.id] = _folded(document, entity, link, base);
  }
  if (stubs.isEmpty) return document;

  return document.copyWith(
    entities: [
      for (final entity in document.entities)
        if (stubs[entity.id] case final stub?)
          stub
        else if (!stubs.containsKey(EntityPath.instanceOf(entity.id)))
          entity,
    ],
  );
}

/// The entity at [id] and everything under it made into a prefab to be saved
/// at [asset], and the entity made an instance of it.
///
/// Everything under it becomes a part, and so takes a path: [PrefabEdit.renamed]
/// says which. It stays where it stood and keeps its name.
///
/// Throws [PrefabException] for something inside an instance, which belongs
/// to that instance's prefab, and for a prefab that would contain itself.
PrefabEdit makePrefab(
  SceneDocument document,
  String id, {
  required String asset,
  required PrefabSource source,
  String? name,
}) {
  if (EntityPath.isPart(id)) {
    throw const PrefabException(
      'That belongs to the prefab it is inside. Make the prefab from the '
      'instance, or unpack the instance first.',
    );
  }
  final made = _prefabOf(
    document,
    id,
    asset: asset,
    source: source,
    name: name,
  );
  PrefabDocument? after(String one) => one == asset ? made.prefab : source(one);
  final linked = _linked(document, id, made, asset, after);
  return PrefabEdit(
    prefab: made.prefab,
    document: linked.document,
    renamed: _renames(document, id, made),
    problems: linked.problems,
  );
}

/// The open instance [id] written into its prefab, and every other instance
/// brought up to date with it.
///
/// This instance's changes become the prefab's. Every other instance keeps
/// its own and picks up the rest, including instances that hold this prefab
/// inside another. The new prefab keeps the old one's ids, its root's name and
/// where its root stands, so every other instance's overrides still land on
/// the parts they were written against.
///
/// Whatever is hung on the instance from outside comes along, and becomes a
/// part: applying is saying "the prefab should look like this", and this is
/// what it looks like.
///
/// Throws [PrefabException] for something that is not an open instance, for
/// an instance inside another one, and when the prefab cannot be read.
PrefabEdit applyInstance(
  SceneDocument document,
  String id, {
  required PrefabSource source,
}) {
  final entity = document[id];
  final link = entity == null ? null : _linkOf(entity);
  if (link == null || link.state != PrefabState.open) {
    throw const PrefabException('That is not an instance of a prefab.');
  }
  if (EntityPath.isPart(id)) {
    throw const PrefabException(
      'That instance belongs to the prefab it is inside. Open that prefab to '
      'change it.',
    );
  }
  final asset = link.asset!;
  final was = _usable(source, asset);
  if (was == null) {
    throw PrefabException('The prefab "$asset" could not be read.');
  }

  final made = _prefabOf(
    document,
    id,
    asset: asset,
    source: source,
    replacing: was,
  );
  PrefabDocument? after(String one) => one == asset ? made.prefab : source(one);
  final linked = _linked(document, id, made, asset, after);
  final others = refreshInstances(
    linked.document,
    asset,
    before: source,
    after: after,
    except: [id],
  );
  return PrefabEdit(
    prefab: made.prefab,
    document: others.document,
    renamed: _renames(document, id, made),
    problems: [...linked.problems, ...others.problems],
  );
}

/// [document] with the open instance [id] reverted: its parts made the
/// prefab's again, and where it stands kept.
///
/// Everything else the instance changed goes. What is hung on it from outside
/// stays where it was, or on the nearest thing still there.
SceneLoad revertInstance(
  SceneDocument document,
  String id,
  PrefabSource source,
) {
  final entity = document[id];
  final link = entity == null ? null : _linkOf(entity);
  if (entity == null || link == null || link.state != PrefabState.open) {
    return SceneLoad(document: document);
  }

  final folded = foldInstances(document, source, only: [id]);
  if (identical(folded, document)) return SceneLoad(document: document);

  final cleared = folded.withEntity(
    id,
    folded[id]!.withComponent(
      SceneComponents.prefab,
      link.copyWith(state: PrefabState.folded, overrides: SceneDiff.none),
    ),
  );
  return _standing(expandInstances(cleared, source, only: [id]), entity);
}

/// [document] with every instance that uses [asset] brought up to date with
/// it, keeping what each one changed.
///
/// [before] is what the instances were opened against and [after] is what
/// they should be opened against now. That includes instances that hold the
/// prefab inside another one: a street of lamps changes when the lamp does.
/// [except] are left as they are.
SceneLoad refreshInstances(
  SceneDocument document,
  String asset, {
  required PrefabSource before,
  required PrefabSource after,
  Iterable<String> except = const [],
}) {
  final skip = except.toSet();
  final opener = _Opener(before, []);
  final affected = [
    for (final entity in document.entities)
      if (_linkOf(entity) case final link?
          when link.state == PrefabState.open &&
              !EntityPath.isPart(entity.id) &&
              !skip.contains(entity.id) &&
              (link.asset == asset || opener.reaches(link.asset!, asset)))
        entity.id,
  ];
  if (affected.isEmpty) return SceneLoad(document: document);

  final folded = foldInstances(document, before, only: affected);
  return expandInstances(folded, after, only: affected);
}

/// [document] with the open instance [id] turned back into ordinary entities
/// that no longer follow the prefab.
///
/// Parts need ids of their own once they stop being parts, so each gets one
/// from [fresh]. A prefab inside the instance stays an instance, one level
/// up — and when the prefab's root was itself an instance of another, so is
/// this. Returns what was renamed, so that anything holding the old ids — a
/// selection — can follow.
({SceneDocument document, Map<String, String> renamed}) unpackInstance(
  SceneDocument document,
  String id, {
  required PrefabSource source,
  required String Function() fresh,
}) {
  final entity = document[id];
  final link = entity == null ? null : _linkOf(entity);
  if (link == null || link.state != PrefabState.open) {
    return (document: document, renamed: const {});
  }

  // Parts of the prefab's root, when that root is an instance of another
  // prefab, sit under a segment no entity has: the root's id in the prefab.
  // They become this entity's parts, and it becomes that instance.
  final base = _usable(source, link.asset!);
  final inner = base == null ? null : _linkOf(base.rootEntity);
  final innerHead = inner == null ? null : base!.root;

  final heads = <String, String>{};
  final renamed = <String, String>{};
  for (final one in document.entities) {
    final rest = EntityPath.localTo(id, one.id, root: '');
    if (rest == null || rest.isEmpty) continue;
    final cut = rest.indexOf(EntityPath.separator);
    final head = cut < 0 ? rest : rest.substring(0, cut);
    if (head == innerHead) {
      if (cut >= 0) {
        renamed[one.id] = EntityPath.join(id, rest.substring(cut + 1));
      }
      continue;
    }
    final mapped = heads.putIfAbsent(head, fresh);
    renamed[one.id] = cut < 0 ? mapped : '$mapped${rest.substring(cut)}';
  }

  final unpacked = inner?.copyWith(
    state: PrefabState.open,
    overrides: SceneDiff.none,
  );
  return (
    document: document.copyWith(
      entities: [
        for (final one in document.entities)
          if (one.id == id)
            one.withComponent(SceneComponents.prefab, unpacked)
          else
            one.copyWith(id: renamed[one.id], parent: renamed[one.parent]),
      ],
    ),
    renamed: renamed,
  );
}

/// The prefab at [asset], unless there is none or it has lost its root.
///
/// A source is the caller's and may hand back anything, so everything here
/// that reaches into a prefab's root asks through this first.
PrefabDocument? _usable(PrefabSource source, String asset) {
  final prefab = source(asset);
  return prefab != null && prefab.document.contains(prefab.root)
      ? prefab
      : null;
}

PrefabComponent? _linkOf(SceneEntity entity) {
  final link = entity[SceneComponents.prefab];
  return link is PrefabComponent && link.asset != null ? link : null;
}

/// [wanted], or the nearest thing to it that is not in [taken] — which it
/// then is.
String _free(String wanted, Set<String> taken) {
  var id = wanted;
  var attempt = 2;
  while (!taken.add(id)) {
    id = '${wanted}_${attempt++}';
  }
  return id;
}

/// [entity] standing at [at], a position as a file writes one.
SceneEntity _positioned(SceneEntity entity, Object? at) {
  final transform = entity[SceneComponents.transform];
  if (transform == null || at == null) return entity;
  return entity.withComponent(
    SceneComponents.transform,
    SceneComponents.read(SceneComponents.transform, {
      ...transform.toJson(),
      'position': at,
    }),
  );
}

Object? _positionOf(SceneEntity? entity) =>
    entity?[SceneComponents.transform]?.toJson()['position'];

/// [opened] with the instance put back where [was] stood.
///
/// Where an instance stands is the one thing about its root that every
/// instance changes, and none of the operations here should move it.
SceneLoad _standing(SceneLoad opened, SceneEntity was) {
  final root = opened.document[was.id];
  if (root == null) return opened;
  return SceneLoad(
    document: opened.document.withEntity(
      was.id,
      _positioned(root, _positionOf(was)),
    ),
    problems: opened.problems,
  );
}

/// A prefab made from a subtree, and the id each entity of the subtree has
/// in it.
class _Made {
  const _Made(this.prefab, this.local);

  final PrefabDocument prefab;
  final Map<String, String> local;
}

/// The entity at [id] and everything under it, as a prefab to be saved at
/// [asset].
///
/// For an apply, [replacing] is the prefab as it was: the new one keeps its
/// ids, its root's name and where its root stands. Instances inside are
/// written folded, as a scene writes them.
_Made _prefabOf(
  SceneDocument document,
  String id, {
  required String asset,
  required PrefabSource source,
  PrefabDocument? replacing,
  String? name,
}) {
  final top = document[id];
  if (top == null) {
    throw const PrefabException('That is not in this scene.');
  }

  final opener = _Opener(source, []);
  final subtree = document.subtreeOf(id);
  final applying = _linkOf(top)?.asset == asset;

  for (final entity in subtree) {
    if (entity.id == id && applying) continue;
    final inner = _linkOf(entity)?.asset;
    if (inner != null && (inner == asset || opener.reaches(inner, asset))) {
      throw PrefabException(
        '"${entity.name}" is an instance of a prefab that would then contain '
        'itself.',
      );
    }
  }

  final root = replacing?.root ?? id;
  final taken = <String>{};
  final local = <String, String>{};
  // The instance's own parts take their ids first, so something hung on it
  // from outside is what gets renamed when two want the same one.
  final ordered = [
    ...subtree.where((e) => EntityPath.within(id, e.id)),
    ...subtree.where((e) => !EntityPath.within(id, e.id)),
  ];
  for (final entity in ordered) {
    final inside = EntityPath.localTo(id, entity.id, root: root);
    final String wanted;
    if (entity.id == id) {
      wanted = root;
    } else if (inside == null) {
      wanted = entity.id;
    } else if (applying) {
      // A part goes back to the id the prefab gave it.
      wanted = inside;
    } else {
      // The root is an instance of something else and stays one, so its
      // parts stay under it.
      wanted = EntityPath.join(root, inside);
    }
    local[entity.id] = _free(wanted, taken);
  }

  final was = replacing?.rootEntity;
  final entities = <SceneEntity>[];
  for (final entity in subtree) {
    if (entity.id == id) continue;
    final parent = entity.parent!;
    entities.add(entity.copyWith(id: local[entity.id], parent: local[parent]));
  }

  var made = SceneEntity(
    id: root,
    name: was?.name ?? name ?? top.name,
    visible: was?.visible ?? top.visible,
    components: top.components,
  );
  if (applying) made = made.withComponent(SceneComponents.prefab, null);
  // A prefab is a thing, not a thing at a place: one saved from something
  // standing forty metres away should not arrive forty metres away. Its
  // rotation and scale are kept, because those are usually part of what the
  // thing is. An existing prefab's root stays where it was, since every
  // instance that never moved stands there.
  made = _positioned(made, _positionOf(was) ?? const [0.0, 0.0, 0.0]);

  final prefabName = replacing?.name ?? name ?? top.name;
  final loose = SceneDocument(name: prefabName, entities: [made, ...entities]);
  return _Made(
    PrefabDocument(
      name: prefabName,
      root: root,
      document: foldInstances(loose, source),
    ),
    local,
  );
}

/// [document] with the subtree at [id] replaced by an instance of [made].
SceneLoad _linked(
  SceneDocument document,
  String id,
  _Made made,
  String asset,
  PrefabSource source,
) {
  final top = document[id]!;
  final gone = {for (final entity in document.subtreeOf(id)) entity.id};
  final stub = SceneEntity(
    id: id,
    name: top.name,
    parent: top.parent,
    visible: top.visible,
    components: {SceneComponents.prefab: PrefabComponent(asset: asset)},
  );
  final linked = document.copyWith(
    entities: [
      for (final entity in document.entities)
        if (entity.id == id) stub else if (!gone.contains(entity.id)) entity,
    ],
  );
  return _standing(expandInstances(linked, source, only: [id]), top);
}

Map<String, String> _renames(SceneDocument document, String id, _Made made) => {
  for (final entity in document.subtreeOf(id))
    if (entity.id != id)
      if (EntityPath.inside(id, made.local[entity.id]!, root: made.prefab.root)
          case final now when now != entity.id)
        entity.id: now,
};

/// The open instance [entity] as the link it is saved as.
SceneEntity _folded(
  SceneDocument document,
  SceneEntity entity,
  PrefabComponent link,
  _Opened base,
) {
  final root = base.root;
  final baseRoot = base.document[root]!;

  // The root's name, place in the tree and visibility are the instance's own
  // and are written on it, so the prefab's stand in for them here and the
  // diff says nothing about them. Its link is not a difference either.
  final components = {...entity.components}..remove(SceneComponents.prefab);
  if (baseRoot[SceneComponents.prefab] case final inner?) {
    components[SceneComponents.prefab] = inner;
  }

  final now = <SceneEntity>[];
  for (final one in document.entities) {
    if (one.id == entity.id) {
      now.add(
        SceneEntity(
          id: root,
          name: baseRoot.name,
          parent: baseRoot.parent,
          visible: baseRoot.visible,
          components: components,
        ),
      );
    } else if (EntityPath.localTo(entity.id, one.id, root: root)
        case final local?) {
      // A part moved out of its instance cannot be said in the instance's
      // terms, so it is kept on the instance's root rather than lost.
      final parent = one.parent;
      now.add(
        one.copyWith(
          id: local,
          parent: parent == null
              ? root
              : (EntityPath.localTo(entity.id, parent, root: root) ?? root),
        ),
      );
    }
  }

  return SceneEntity(
    id: entity.id,
    name: entity.name,
    parent: entity.parent,
    visible: entity.visible,
    components: {
      SceneComponents.prefab: link.copyWith(
        state: PrefabState.folded,
        overrides: SceneDiff.between(
          base.document,
          base.document.copyWith(entities: now),
        ),
      ),
    },
  );
}

/// A prefab opened in its own ids, ready to be put in under an instance's.
class _Opened {
  const _Opened(this.root, this.document);

  final String root;

  /// Open all the way down.
  final SceneDocument document;
}

/// Opens prefabs, each once, and notices one that contains itself.
class _Opener {
  _Opener(this.source, this.problems);

  final PrefabSource source;
  final List<String> problems;

  final Map<String, _Opened?> _opened = {};
  final Set<String> _opening = {};

  _Opened? open(String asset) {
    if (_opened.containsKey(asset)) return _opened[asset];
    if (!_opening.add(asset)) {
      problems.add(
        '"$asset" contains an instance of itself, which was left closed.',
      );
      return null;
    }
    try {
      final prefab = _usable(source, asset);
      if (prefab == null) {
        problems.add(
          'The prefab "$asset" could not be read, so its instances are links '
          'with nothing in them until it can.',
        );
        return _opened[asset] = null;
      }
      return _opened[asset] = _Opened(prefab.root, openAll(prefab.document));
    } finally {
      _opening.remove(asset);
    }
  }

  /// Whether the prefab at [from] contains [target], at any depth.
  bool reaches(String from, String target, [Set<String>? walked]) {
    final seen = walked ?? <String>{};
    if (!seen.add(from)) return false;
    final prefab = source(from);
    if (prefab == null) return false;
    for (final entity in prefab.document.entities) {
      final inner = _linkOf(entity)?.asset;
      if (inner == null) continue;
      if (inner == target || reaches(inner, target, seen)) return true;
    }
    return false;
  }

  SceneDocument openAll(SceneDocument document, {Set<String>? only}) {
    final relinked = _relink(document, only);
    final out = <SceneEntity>[];
    var changed = !identical(relinked, document);

    for (final entity in relinked.entities) {
      final link = _linkOf(entity);
      if (link == null ||
          link.state != PrefabState.folded ||
          EntityPath.isPart(entity.id) ||
          (only != null && !only.contains(entity.id))) {
        out.add(entity);
        continue;
      }

      // Parts already here mean it was written open and never folded. They
      // are what it looked like, so they are kept rather than doubled.
      if (relinked.entities.any(
        (one) => one.id != entity.id && EntityPath.within(entity.id, one.id),
      )) {
        out.add(
          entity.withComponent(
            SceneComponents.prefab,
            link.copyWith(state: PrefabState.open, overrides: SceneDiff.none),
          ),
        );
        changed = true;
        continue;
      }

      final base = open(link.asset!);
      if (base == null) {
        out.add(entity);
        continue;
      }
      out.addAll(_parts(entity, link, base));
      changed = true;
    }

    final tree = rooted(
      out,
      problems,
      closed: {
        for (final entity in out)
          if (_linkOf(entity)?.state == PrefabState.folded) entity.id,
      },
    );
    if (!changed && identical(tree, out)) return document;
    return relinked.copyWith(entities: tree);
  }

  /// The folded instance [stub], opened against [base].
  Iterable<SceneEntity> _parts(
    SceneEntity stub,
    PrefabComponent link,
    _Opened base,
  ) sync* {
    final root = base.root;
    // The root's name, place and visibility belong to the stub, and it cannot
    // be taken out of its own instance. Nothing folding writes says otherwise,
    // but a hand-edited file might.
    final overrides = SceneDiff([
      for (final op in link.overrides.operations)
        if (!(op is RemoveEntity && op.id == root) &&
            !(op is Reparent && op.id == root))
          op,
    ]);

    final added = {
      for (final op in overrides.operations)
        if (op is AddEntity) op.id,
    };
    final lost = overrides.operations.where((op) {
      final target = _target(op);
      return target != null &&
          !base.document.contains(target) &&
          !added.contains(target);
    }).length;
    if (lost > 0) {
      problems.add(
        lost == 1
            ? 'One change to "${stub.name}" was to a part its prefab no '
                  'longer has, and was let go.'
            : '$lost changes to "${stub.name}" were to parts its prefab no '
                  'longer has, and were let go.',
      );
    }

    for (final entity in overrides.applyTo(base.document).entities) {
      if (entity.id == root) {
        yield SceneEntity(
          id: stub.id,
          name: stub.name,
          parent: stub.parent,
          visible: stub.visible,
          components: {
            ...entity.components,
            SceneComponents.prefab: link.copyWith(
              state: PrefabState.open,
              overrides: SceneDiff.none,
            ),
          },
        );
        continue;
      }
      final parent = entity.parent;
      yield entity.copyWith(
        id: EntityPath.join(stub.id, entity.id),
        parent: parent == null
            ? stub.id
            : EntityPath.inside(stub.id, parent, root: root),
      );
    }
  }

  /// [document] with its stamped copies turned into open instances.
  ///
  /// A copy's parts are paired with the prefab's by walking both trees in
  /// the same order, among the children that carry the copy's mark — which is
  /// how they were made, and so where the shape has not changed since, every
  /// part finds itself. A part the prefab no longer has, or a thing somebody
  /// added under the copy, becomes this document's own. A part of the prefab
  /// the copy no longer has is one it removed.
  SceneDocument _relink(SceneDocument document, Set<String>? only) {
    bool stampedFrom(SceneEntity? entity, String asset) {
      final link = entity == null ? null : _linkOf(entity);
      return link != null &&
          link.state == PrefabState.stamped &&
          link.asset == asset;
    }

    final renamed = <String, String>{};
    final opened = <String>{};
    final released = <String>{};
    final counts = <String, int>{};

    for (final entity in document.entities) {
      final link = _linkOf(entity);
      if (link == null || link.state != PrefabState.stamped) continue;
      final asset = link.asset!;
      final parent = entity.parent;
      if (parent != null && stampedFrom(document[parent], asset)) continue;
      if (only != null && !only.contains(entity.id)) continue;

      final base = open(asset);
      if (base == null) continue;

      void pair(String mine, String theirs) {
        final ours = [
          for (final one in document.childrenOf(mine))
            if (stampedFrom(one, asset)) one,
        ];
        final prefabs = base.document.childrenOf(theirs);
        for (var i = 0; i < ours.length && i < prefabs.length; i++) {
          renamed[ours[i].id] = EntityPath.join(entity.id, prefabs[i].id);
          pair(ours[i].id, prefabs[i].id);
        }
      }

      pair(entity.id, base.root);
      opened.add(entity.id);
      counts[asset] = (counts[asset] ?? 0) + 1;
      for (final one in document.subtreeOf(entity.id)) {
        if (one.id != entity.id && stampedFrom(one, asset)) {
          released.add(one.id);
        }
      }
    }
    if (opened.isEmpty) return document;

    for (final MapEntry(key: asset, value: count) in counts.entries) {
      problems.add(
        count == 1
            ? 'An instance of "$asset" was a copy of it, and is now linked to '
                  'it. What was changed about it is kept.'
            : '$count instances of "$asset" were copies of it, and are now '
                  'linked to it. What was changed about each is kept.',
      );
    }

    return document.copyWith(
      entities: [
        for (final entity in document.entities)
          if (opened.contains(entity.id))
            entity.withComponent(
              SceneComponents.prefab,
              _linkOf(entity)!.copyWith(state: PrefabState.open),
            )
          else if (released.contains(entity.id))
            entity
                .withComponent(SceneComponents.prefab, null)
                .copyWith(
                  id: renamed[entity.id],
                  parent: renamed[entity.parent],
                )
          else
            entity.copyWith(parent: renamed[entity.parent]),
      ],
    );
  }

  /// The entity an operation changes, for the ones that change one that has
  /// to be there already.
  static String? _target(SceneOp op) => switch (op) {
    SetEntityName(:final id) ||
    SetVisible(:final id) ||
    SetComponent(:final id) ||
    SetField(:final id) ||
    Reparent(:final id) ||
    Reorder(:final id) => id,
    _ => null,
  };
}
