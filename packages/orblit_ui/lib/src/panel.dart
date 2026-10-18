import 'dart:ui' as ui;

import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';

/// A widget tree drawn without a window.
///
/// A screen on a wall, a name above a head and a health bar over an enemy are
/// interfaces that belong to a place in the scene and not to the edge of the
/// view. This owns the tree that draws one: its own build and render pipelines,
/// a fixed [size], and nothing on screen. [paint] returns the picture as PNG
/// bytes. [send] takes a pointer in the panel's own pixels, so whatever puts
/// the picture on a surface can turn a click on the surface into a click here.
///
/// The tree is real Flutter, so a [UiSurface] or any widget goes in. It
/// animates and answers events like any other. What it does not get is a
/// window: no keyboard focus, no hover cursor and no overlay, so a text field
/// or a tooltip has nothing to attach to.
///
/// It notifies when the picture is out of date and does not draw by itself.
/// Drawing costs a read-back of the image, so the caller decides how often,
/// usually once a frame at most and only after a notification.
final class UiPanel extends ChangeNotifier {
  UiPanel({
    required Widget child,
    required this.size,
    this.pixelRatio = 1,
    ui.FlutterView? view,
  }) : assert(size.width >= 1 && size.height >= 1),
       _build = BuildOwner(focusManager: FocusManager()) {
    _build.onBuildScheduled = _markDirty;
    _view = RenderView(
      view: view ?? ui.PlatformDispatcher.instance.views.first,
      configuration: ViewConfiguration(
        logicalConstraints: BoxConstraints.tight(size),
        physicalConstraints: BoxConstraints.tight(size * pixelRatio),
        devicePixelRatio: pixelRatio,
      ),
      child: _boundary,
    );
    _pipeline.rootNode = _view;
    _view.prepareInitialFrame();
    _root = _adapter(child).attachToRenderTree(_build);
  }

  /// The panel's size in logical pixels. Pointer positions use this space.
  final Size size;

  /// How many picture pixels one logical pixel becomes.
  final double pixelRatio;

  final BuildOwner _build;
  late final PipelineOwner _pipeline = PipelineOwner(
    onNeedVisualUpdate: _markDirty,
  );
  final RenderRepaintBoundary _boundary = RenderRepaintBoundary();
  late final RenderView _view;
  late final RenderObjectToWidgetElement<RenderBox> _root;

  /// Each pressed pointer, from where it first landed. A drag keeps going to
  /// the tree it began on, however far the pointer travels from it.
  final Map<int, _Press> _pressed = {};

  bool _dirty = true;
  bool _disposed = false;

  /// Whether the picture has changed since [paint] last drew it.
  bool get isDirty => _dirty;

  /// Replaces what the panel shows.
  void update(Widget child) {
    _adapter(child).attachToRenderTree(_build, _root);
  }

  /// Builds, lays out and draws the tree, then returns it as PNG bytes.
  Future<Uint8List> paint() async {
    _build.buildScope(_root);
    _pipeline
      ..flushLayout()
      ..flushCompositingBits()
      ..flushPaint();
    _build.finalizeTree();
    _dirty = false;
    final image = await _boundary.toImage(pixelRatio: pixelRatio);
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    image.dispose();
    return data!.buffer.asUint8List();
  }

  /// Delivers a pointer event, with [PointerEvent.position] in the panel's
  /// pixels. Press, move, release and cancel are delivered. Hover is not,
  /// because the mouse tracker belongs to a real window.
  ///
  /// A move's `delta` is filled in here from the last position, because a
  /// caller that turns rays into positions has no previous event to hand over.
  void send(PointerEvent event) {
    final press = event is PointerDownEvent
        ? (_pressed[event.pointer] = _Press(
            _hit(event.position),
            event.position,
          ))
        : _pressed[event.pointer];
    if (press == null) return;
    final delivered = event is PointerMoveEvent
        ? event.copyWith(delta: event.position - press.at)
        : event;
    press.at = event.position;
    if (event is PointerUpEvent || event is PointerCancelEvent) {
      _pressed.remove(event.pointer);
    }
    GestureBinding.instance.dispatchEvent(delivered, press.path);
  }

  HitTestResult _hit(Offset position) {
    final result = HitTestResult();
    _view.hitTest(result, position: position);
    // The binding closes and sweeps the gesture arena. It is last in every
    // path a real view builds, and a tap never completes without it.
    result.add(HitTestEntry(GestureBinding.instance));
    return result;
  }

  void _markDirty() {
    if (_disposed) return;
    _dirty = true;
    notifyListeners();
  }

  RenderObjectToWidgetAdapter<RenderBox> _adapter(Widget? child) =>
      RenderObjectToWidgetAdapter<RenderBox>(
        container: _boundary,
        child: child == null
            ? null
            : MediaQuery(
                data: MediaQueryData(size: size, devicePixelRatio: pixelRatio),
                child: Directionality(
                  textDirection: TextDirection.ltr,
                  child: Material(
                    type: MaterialType.transparency,
                    child: child,
                  ),
                ),
              ),
      );

  @override
  void dispose() {
    _adapter(null).attachToRenderTree(_build, _root);
    _build
      ..buildScope(_root)
      ..finalizeTree();
    _disposed = true;
    _pipeline.rootNode = null;
    _view.dispose();
    _build.focusManager.dispose();
    _pipeline.dispose();
    super.dispose();
  }
}

final class _Press {
  _Press(this.path, this.at);

  final HitTestResult path;
  Offset at;
}
