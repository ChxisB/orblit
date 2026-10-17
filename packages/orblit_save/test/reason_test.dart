import 'package:orblit_save/orblit_save.dart';
import 'package:test/test.dart';

void main() {
  test('every reason reads back from its label', () {
    final reasons = <SaveReason>[
      const Autosave(),
      const Leave(),
      const Disconnect(),
      const Shutdown(),
      const Failed(),
      Custom('pre-restore'),
    ];

    for (final reason in reasons) {
      expect(SaveReason.parse(reason.label), reason);
    }
    expect(reasons.map((r) => r.label).toSet(), hasLength(reasons.length));
  });

  test('a label that is not a reason throws', () {
    expect(() => SaveReason.parse('nap'), throwsFormatException);
    expect(() => SaveReason.parse(''), throwsFormatException);
  });

  test('a custom reason keeps its text and refuses an empty or long one', () {
    expect(SaveReason.parse('custom:pre-restore'), isA<Custom>());
    expect(
      (SaveReason.parse('custom:pre-restore') as Custom).text,
      'pre-restore',
    );
    expect(() => Custom(''), throwsArgumentError);
    expect(() => Custom('a' * 65), throwsArgumentError);
    expect(() => Custom('a\nb'), throwsArgumentError);
    expect(Custom('a' * 64).text, hasLength(64));
  });

  test('a custom reason is not an engine reason of the same name', () {
    expect(Custom('autosave'), isNot(const Autosave()));
  });
}
