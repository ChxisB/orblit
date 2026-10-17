import 'binding.dart';

/// A set of bindings that is switched on and off as a whole: gameplay, a
/// menu, a vehicle.
final class InputContext {
  InputContext(
    this.name, {
    required Map<String, List<Binding>> bindings,
    this.blocks = false,
  }) : bindings = Map.unmodifiable({
         for (final MapEntry(:key, :value) in bindings.entries)
           key: List<Binding>.unmodifiable(value),
       });

  final String name;

  /// The default bindings, by action name.
  final Map<String, List<Binding>> bindings;

  /// Whether the contexts under this one go quiet while it is on. A menu
  /// blocks, so the jump key does not jump behind it. A vehicle does not, so
  /// the pause key still works.
  final bool blocks;
}
