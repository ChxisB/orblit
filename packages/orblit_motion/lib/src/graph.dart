import 'blend.dart';
import 'source.dart';

List<BlendState> expandedStates(List<BlendState> states) => [
  for (final state in states)
    if (state.plays case BlendGraph(:final graph))
      for (final leaf in graph.states)
        BlendState(
          '${state.name}/${leaf.name}',
          plays: leaf.plays,
          speed: state.speed * leaf.speed,
          whenDone: leaf.whenDone ?? state.whenDone,
          sync: leaf.sync.isEmpty ? state.sync : leaf.sync,
        )
    else
      state,
];

Map<String, String> graphEntries(List<BlendState> states) => {
  for (final state in states)
    if (state.plays case BlendGraph(:final graph)) ...{
      state.name: '${state.name}/${graph.start}',
      for (final MapEntry(:key, :value) in graph.entries.entries)
        '${state.name}/$key': '${state.name}/$value',
    },
};

Map<String, double> graphInputs(List<BlendState> states) => {
  for (final state in states)
    if (state.plays case BlendGraph(:final graph)) ...graph.inputs,
};

List<BlendChange> expandedChanges(
  List<BlendState> states,
  List<BlendChange> changes,
) {
  final entries = graphEntries(states);
  final leaves = expandedStates(states);
  final out = [
    for (final change in changes) ..._outerChanges(change, entries, leaves),
  ];
  for (final state in states) {
    if (state.plays case BlendGraph(:final graph)) {
      out.addAll(_innerChanges(state.name, graph));
    }
  }
  return List.unmodifiable(out);
}

List<BlendChange> _outerChanges(
  BlendChange change,
  Map<String, String> entries,
  List<BlendState> leaves,
) {
  final from = change.from;
  final sources = from != null && entries.containsKey(from)
      ? [
          for (final leaf in leaves)
            if (leaf.name.startsWith('$from/')) leaf.name,
        ]
      : [from];
  final to = entries[change.to] ?? change.to;
  return [
    for (final source in sources)
      source == from && to == change.to
          ? change
          : copyChange(change, from: source, to: to),
  ];
}

List<BlendChange> _innerChanges(String name, BlendDocument graph) => [
  for (final change in graph.changes)
    for (final source
        in change.from == null
            ? [
                for (final leaf in graph.states)
                  if (leaf.name != change.to) leaf.name,
              ]
            : [change.from])
      copyChange(change, from: '$name/$source', to: '$name/${change.to}'),
];

BlendChange copyChange(
  BlendChange change, {
  required String? from,
  required String to,
}) => BlendChange(
  from: from,
  to: to,
  when: change.when,
  fade: change.fade,
  shape: change.shape,
  inStep: change.inStep,
  fromPose: change.fromPose,
);
