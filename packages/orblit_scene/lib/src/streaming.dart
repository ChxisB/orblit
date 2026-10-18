import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_cells/orblit_cells.dart';

import 'diff.dart';
import 'document.dart';

/// What one cell cost to bring in. The record a profile of loads is made of.
final class CellLoad {
  const CellLoad({
    required this.cell,
    required this.fetch,
    required this.apply,
    required this.bytes,
    required this.entities,
    this.error,
  });

  final Cell cell;

  /// From asking for the bytes to having them.
  final Duration fetch;

  /// Decoding and applying, which is the part that happens inside a frame.
  final Duration apply;

  final int bytes;
  final int entities;

  /// Why the cell did not load, or null when it did.
  final Object? error;
}

/// What one [CellStreamer.step] did.
final class CellStep {
  const CellStep({
    this.spent = Duration.zero,
    this.loaded = 0,
    this.dropped = 0,
  });

  final Duration spent;
  final int loaded;
  final int dropped;
}

/// Keeps the cells of a world that are near a point in a scene, and no others.
///
/// A cell is a scene file. It is fetched as bytes, read, and put into the scene
/// as one diff. Every entity in it takes the cell as a prefix on its id (see
/// [idIn]), so two cells never clash, and the diff is handed to [apply]. The
/// caller sends that diff wherever the scene is kept: the document view that
/// draws it, the physics that gives its bodies a world, or both. A body joins
/// when its cell loads and leaves when it goes, because that is what a diff
/// that adds and removes an entity already does to them.
///
/// A cell file is written in world coordinates. Nothing is moved when it is
/// put in, so it is tied to its place.
///
/// Work in a frame is limited by [budget]. The time is read between cells, so
/// one cell is never split: a frame can run over by the cost of one cell, and
/// at least one is done in every frame that has something to do.
final class CellStreamer {
  CellStreamer({
    required this.window,
    required this.fetch,
    required this.apply,
    this.onLoad,
    this.budget = const Duration(milliseconds: 4),
    this.inFlight = 2,
    this.retryAfter = const Duration(seconds: 5),
    Duration Function()? clock,
  }) : _clock = clock ?? _monotonic() {
    if (budget <= Duration.zero) {
      throw ArgumentError.value(budget, 'budget', 'not above zero');
    }
    if (inFlight < 1) {
      throw ArgumentError.value(inFlight, 'inFlight', 'not above zero');
    }
  }

  final CellWindow window;

  /// The bytes of the scene file for a cell, or null when the world has
  /// nothing there. A throw is a cell that could not be read, and is asked for
  /// again after [retryAfter].
  final Future<Uint8List?> Function(Cell cell) fetch;

  /// Where a cell's diff goes.
  final void Function(SceneDiff diff) apply;

  /// Told about every cell that loaded or failed to.
  final void Function(CellLoad load)? onLoad;

  /// The most time a frame spends reading and applying cells.
  final Duration budget;

  /// The most cells fetched or waiting to be applied at one time. Bounds the
  /// memory a slow frame can pile up.
  final int inFlight;

  final Duration retryAfter;

  final Duration Function() _clock;

  /// The id an entity written as [id] in the file for [cell] has in the scene.
  static String idIn(Cell cell, String id) => 'cell_${cell.x}_${cell.z}:$id';

  /// The ids each loaded cell added, so it can take them out again. An empty
  /// list is a cell the world has nothing in, which is loaded all the same.
  final Map<Cell, List<String>> _loaded = {};
  final Set<Cell> _fetching = {};
  final List<_Fetched> _ready = [];
  final Map<Cell, Duration> _failedUntil = {};

  CellStep _last = const CellStep();

  Iterable<Cell> get loaded => _loaded.keys;

  bool isLoaded(Cell cell) => _loaded.containsKey(cell);

  /// Cells asked for and not yet in the scene.
  int get pending => _fetching.length + _ready.length;

  /// What the most recent [step] did.
  CellStep get lastStep => _last;

  /// Moves the window to ([x], [z]), facing ([facingX], [facingZ]): drops the
  /// cells it has left, applies the ones that have arrived and asks for the
  /// next. Call it once a frame. A [pinned] cell is loaded and kept wherever
  /// it is.
  void step(
    double x,
    double z, {
    double facingX = 0,
    double facingZ = 0,
    Set<Cell> pinned = const {},
  }) {
    final focus = window.grid.cellOf(x, z);
    if (focus == null) return;
    final began = _clock();
    _failedUntil.removeWhere((_, until) => until <= began);
    final plan = window.plan(
      x,
      z,
      facingX: facingX,
      facingZ: facingZ,
      loaded: _loaded.keys.toSet(),
      pinned: pinned,
    );
    bool over() => _clock() - began >= budget;

    var dropped = 0;
    for (final cell in plan.drop) {
      if (over()) break;
      _drop(cell);
      dropped++;
    }

    final wanted = {for (var i = 0; i < plan.load.length; i++) plan.load[i]: i};
    _ready.removeWhere(
      (one) => !window.keeps(one.cell, focus) && !pinned.contains(one.cell),
    );
    _ready.sort(
      (a, b) => (wanted[a.cell] ?? plan.load.length).compareTo(
        wanted[b.cell] ?? plan.load.length,
      ),
    );
    var loaded = 0;
    while (_ready.isNotEmpty && !over()) {
      _admit(_ready.removeAt(0));
      loaded++;
    }

    for (final cell in plan.load) {
      if (pending >= inFlight) break;
      if (!_busy(cell)) unawaited(_ask(cell));
    }
    _last = CellStep(spent: _clock() - began, loaded: loaded, dropped: dropped);
  }

  // Includes the cells loaded in this very step, which the plan was made
  // before and still lists.
  bool _busy(Cell cell) =>
      _loaded.containsKey(cell) ||
      _fetching.contains(cell) ||
      _failedUntil.containsKey(cell) ||
      _ready.any((one) => one.cell == cell);

  Future<void> _ask(Cell cell) async {
    _fetching.add(cell);
    final asked = _clock();
    try {
      final bytes = await fetch(cell);
      _ready.add(_Fetched(cell, bytes, _clock() - asked));
    } on Object catch (error) {
      // The fetcher is the caller's code and may throw anything. A cell that
      // cannot be read is not a reason to stop the world loading.
      _fail(cell, error, _clock() - asked);
    } finally {
      _fetching.remove(cell);
    }
  }

  void _admit(_Fetched fetched) {
    final began = _clock();
    final cell = fetched.cell;
    final bytes = fetched.bytes;
    final SceneDocument document;
    try {
      document = bytes == null
          ? SceneDocument()
          : SceneDocument.decode(utf8.decode(bytes)).document;
    } on Exception catch (error) {
      // A file that is not a scene reads as a cell that failed. Both the text
      // and the scene reader throw an Exception.
      _fail(cell, error, fetched.took);
      return;
    }
    final ids = <String>[];
    final adds = <SceneOp>[];
    for (final entity in document.entities) {
      final parent = entity.parent;
      final id = idIn(cell, entity.id);
      adds.add(
        AddEntity(
          entity
              .copyWith(
                id: id,
                parent: parent == null ? null : idIn(cell, parent),
              )
              .toJson(),
          after: ids.isEmpty ? null : ids.last,
        ),
      );
      ids.add(id);
    }
    if (adds.isNotEmpty) apply(SceneDiff(adds));
    _loaded[cell] = ids;
    onLoad?.call(
      CellLoad(
        cell: cell,
        fetch: fetched.took,
        apply: _clock() - began,
        bytes: bytes?.length ?? 0,
        entities: ids.length,
      ),
    );
  }

  void _drop(Cell cell) {
    // The plan lists only loaded cells, so the ids are there.
    final ids = _loaded.remove(cell)!;
    if (ids.isEmpty) return;
    apply(
      SceneDiff([
        for (final id in ids) RemoveEntity({'id': id}),
      ]),
    );
  }

  void _fail(Cell cell, Object error, Duration took) {
    _failedUntil[cell] = _clock() + retryAfter;
    onLoad?.call(
      CellLoad(
        cell: cell,
        fetch: took,
        apply: Duration.zero,
        bytes: 0,
        entities: 0,
        error: error,
      ),
    );
  }
}

final class _Fetched {
  const _Fetched(this.cell, this.bytes, this.took);

  final Cell cell;
  final Uint8List? bytes;
  final Duration took;
}

Duration Function() _monotonic() {
  final watch = Stopwatch()..start();
  return () => watch.elapsed;
}
