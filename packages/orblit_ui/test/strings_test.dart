import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_ui/orblit_ui.dart';

UiStrings _menu() => UiStrings()
  ..add('en', {
    'play': 'Play',
    'greet': 'Hello, {name}',
    'items.one': '{count} item',
    'items.other': '{count} items',
  })
  ..add('fr', {'play': 'Jouer', 'greet': 'Bonjour, {name}'})
  ..add('fr-ca', {'play': 'Joue'});

UiNode _keyed(String key, [Map<String, Object?> args = const {}]) => UiNode(
  type: 'text',
  props: {'textKey': key, if (args.isNotEmpty) 'textArgs': args},
);

void main() {
  group('a key is looked up from the language outwards', () {
    test('a regional language, then its language, then the default', () {
      final strings = _menu()..use('fr_CA');
      expect(strings.locale, 'fr-ca');
      expect(strings.text('play'), 'Joue');
      expect(strings.text('greet', {'name': 'Ann'}), 'Bonjour, Ann');
      expect(strings.text('items.other', {'count': 2}), '2 items');
    });

    test('a key nobody has is answered as itself', () {
      expect(_menu().text('quit'), 'quit');
    });

    test('arguments fill their names and leave unknown ones alone', () {
      final strings = _menu();
      expect(strings.text('greet', {'name': 'Ann'}), 'Hello, Ann');
      expect(strings.text('greet'), 'Hello, {name}');
      expect(strings.text('greet', {'name': null}), 'Hello, {name}');
    });

    test('find is null for a key nobody has', () {
      expect(_menu().find('quit'), isNull);
      expect(_menu().find('play'), 'Play');
    });

    test('every shorter tag is tried, and the default has its own', () {
      final strings = UiStrings(locale: 'zh-Hant-TW', fallback: 'en-GB')
        ..add('zh-hant', {'play': '玩'})
        ..add('en', {'quit': 'Quit'});
      expect(strings.chain('zh-hant-tw'), [
        'zh-hant-tw',
        'zh-hant',
        'zh',
        'en-gb',
        'en',
      ]);
      expect(strings.text('play'), '玩');
      expect(strings.text('quit'), 'Quit');
    });
  });

  group('plurals', () {
    test('a count picks the form its language gives it', () {
      const forms = {
        'en': {0: 'other', 1: 'one', 2: 'other'},
        'fr': {0: 'one', 1: 'one', 2: 'other'},
        'ja': {1: 'other', 5: 'other'},
        'ru': {1: 'one', 2: 'few', 5: 'many', 11: 'many', 21: 'one', 22: 'few'},
        'pl': {1: 'one', 2: 'few', 5: 'many', 12: 'many', 22: 'few'},
        'cs': {1: 'one', 3: 'few', 5: 'other'},
        'ar': {
          0: 'zero',
          1: 'one',
          2: 'two',
          7: 'few',
          15: 'many',
          100: 'other',
        },
      };
      for (final MapEntry(key: language, value: counts) in forms.entries) {
        for (final MapEntry(key: count, value: form) in counts.entries) {
          expect(
            UiStrings.pluralForm(language, count),
            form,
            reason: '$count in $language',
          );
        }
      }
    });

    test('a count chooses between the keys of a table', () {
      final strings = _menu();
      expect(strings.text('items', {'count': 1}), '1 item');
      expect(strings.text('items', {'count': 3}), '3 items');
    });

    test('a count with no fraction is a whole count', () {
      final strings = _menu();
      expect(strings.text('items', {'count': 1.0}), '1.0 item');
      expect(strings.text('items', {'count': 3.0}), '3.0 items');
      expect(strings.text('items', {'count': 1.5}), 'items');
    });

    test('pt-PT counts zero as other and pt as one', () {
      expect(UiStrings.pluralForm('pt-PT', 0), 'other');
      expect(UiStrings.pluralForm('pt-BR', 0), 'one');
    });

    test('a text found in the default takes the default\'s plurals', () {
      final strings = _menu()..use('ru');
      // 21 is one in Russian and other in English, so the wrong rule shows.
      expect(strings.text('items', {'count': 21}), '21 items');
    });
  });

  group('a language file', () {
    test('is one object of text', () {
      final strings = UiStrings()..addJson('de', '{"play": "Spielen"}');
      strings.use('de');
      expect(strings.text('play'), 'Spielen');
      for (final bad in ['[]', '{"play": 3}', '{"play"']) {
        expect(() => strings.addJson('de', bad), throwsFormatException);
      }
    });

    test('can be dropped, which tells a listener', () {
      final strings = _menu();
      var heard = 0;
      strings.addListener(() => heard++);
      strings.clear();
      expect(strings.locales, isEmpty);
      expect(heard, 1);
    });

    test('has a tag that is not empty', () {
      expect(() => UiStrings(locale: ' '), throwsArgumentError);
    });
  });

  group('the gaps a translator fills', () {
    test('lists the keys each language has no text for', () {
      final strings = _menu()..add('de', {'items.other': 'Dinge'});
      expect(strings.gaps(['quit']), {
        'de': ['greet', 'play', 'quit'],
        'en': ['quit'],
        'fr': ['items', 'quit'],
        'fr-ca': ['items', 'quit'],
      });
    });

    test('counts a used key that looks like a plural form as written', () {
      final strings = UiStrings()..add('en', {'settings.other': 'Other'});
      expect(strings.gaps(['settings.other']), isEmpty);
    });

    test('is empty when every language has every key', () {
      expect(UiStrings().gaps(), isEmpty);
    });
  });

  group('a text node', () {
    test('names its keys', () {
      final tree = UiNode(
        type: 'column',
        children: [
          _keyed('play'),
          const UiNode(type: 'text', text: 'Fixed'),
          const UiNode(type: 'button', props: {'textKey': 'quit'}),
        ],
      );
      expect(tree.textKeys, {'play', 'quit'});
    });
  });

  group('a live interface', () {
    Future<void> show(WidgetTester tester, UiNode tree, UiStrings? strings) =>
        tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: UiSurface(description: tree, strings: strings),
            ),
          ),
        );

    testWidgets('changes language with the same description', (tester) async {
      final strings = _menu();
      final tree = UiNode(
        type: 'column',
        children: [
          _keyed('greet', {'name': 'Ann'}),
          const UiNode(type: 'button', props: {'textKey': 'play'}),
        ],
      );
      await show(tester, tree, strings);
      expect(find.text('Hello, Ann'), findsOneWidget);
      expect(find.text('Play'), findsOneWidget);

      strings.use('fr');
      await tester.pump();
      expect(find.text('Bonjour, Ann'), findsOneWidget);
      expect(find.text('Jouer'), findsOneWidget);
    });

    testWidgets('shows a missing key, not a blank', (tester) async {
      await show(tester, _keyed('quit'), _menu());
      expect(find.text('quit'), findsOneWidget);
      await show(tester, _keyed('quit'), null);
      expect(find.text('quit'), findsOneWidget);
    });

    testWidgets('shows its own text before a key nobody has', (tester) async {
      const tree = UiNode(
        type: 'text',
        text: 'Quit game',
        props: {'textKey': 'quit'},
      );
      await show(tester, tree, _menu());
      expect(find.text('Quit game'), findsOneWidget);
      await show(tester, tree, null);
      expect(find.text('Quit game'), findsOneWidget);
    });

    testWidgets('sets text in the fallback fonts it was given', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: UiSurface(
            description: UiNode(type: 'text', text: 'Hi'),
            fontFamily: 'Main',
            fontFallback: ['Noto Sans JP'],
          ),
        ),
      );
      final style = tester.widget<Text>(find.text('Hi')).style!;
      expect(style.fontFamilyFallback, ['Noto Sans JP']);
    });
  });
}
