import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_examples/orblit_examples.dart';

void main() {
  test('the engine examples are listed in the order of their headings', () {
    // A host that lists them flat, as the editor does, and one that lists them
    // under headings, as the gallery does, should read the same way down.
    final flat = engineExamples();
    final grouped = [
      for (final (_, examples) in examplesBySection(flat)) ...examples,
    ];

    expect(
      grouped.map((e) => e.name).toList(),
      flat.map((e) => e.name).toList(),
    );
  });

  test('a heading with nothing under it is left out', () {
    final sections = [
      for (final (section, _) in examplesBySection(engineExamples())) section,
    ];

    // The scripting examples live in the gallery, beside the runtime they
    // need, so a host with only this package has nothing to put there.
    expect(sections, [
      for (final section in ExampleSection.values)
        if (section != ExampleSection.scripting) section,
    ]);
  });
}
