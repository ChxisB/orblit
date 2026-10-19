import 'dart:convert';

import 'package:flutter/foundation.dart';

/// Which plural form a count takes, named as the Unicode CLDR names them.
typedef _PluralRule = String Function(int count);

String _other(int _) => 'other';

String _oneOrOther(int n) => n == 1 ? 'one' : 'other';

String _zeroOrOne(int n) => n == 0 || n == 1 ? 'one' : 'other';

/// Eleven to fourteen, which take the many form in the Slavic rules whatever
/// their last digit is.
bool _teen(int n) => n % 100 >= 11 && n % 100 <= 14;

bool _fewEnding(int n) => n % 10 >= 2 && n % 10 <= 4 && !_teen(n);

String _eastSlavic(int n) {
  if (n % 10 == 1 && n % 100 != 11) return 'one';
  return _fewEnding(n) ? 'few' : 'many';
}

String _polish(int n) {
  if (n == 1) return 'one';
  return _fewEnding(n) ? 'few' : 'many';
}

String _czech(int n) {
  if (n == 1) return 'one';
  return n >= 2 && n <= 4 ? 'few' : 'other';
}

String _arabic(int n) {
  final last = n % 100;
  if (n == 0) return 'zero';
  if (n == 1) return 'one';
  if (n == 2) return 'two';
  if (last >= 3 && last <= 10) return 'few';
  return last >= 11 ? 'many' : 'other';
}

/// The languages whose plurals are not English's. Any other language takes
/// one and other.
final Map<String, _PluralRule> _rules = {
  for (final language in ['ja', 'zh', 'ko', 'vi', 'th', 'id']) language: _other,
  for (final language in ['fr', 'pt', 'hi']) language: _zeroOrOne,
  for (final language in ['ru', 'uk']) language: _eastSlavic,
  'pl': _polish,
  'cs': _czech,
  'sk': _czech,
  'ar': _arabic,
};

/// The words an interface shows, by key, in as many languages as were added.
///
/// A table is a map from key to text. A text node names its key and gives the
/// arguments, and [text] answers in the language in use. A key a language
/// lacks is looked up in its parent language and then in the default, and a
/// key nobody has is answered as itself, so a missing translation shows as a
/// name a translator can search for rather than a blank.
///
/// A listener hears every change of language or table, which is how a live
/// interface redraws with no new description.
final class UiStrings extends ChangeNotifier {
  /// Starts in [locale], with [fallback] as the language of last resort.
  /// Throws [ArgumentError] for an empty tag.
  UiStrings({String locale = 'en', String fallback = 'en'})
    : fallback = tagOf(fallback),
      _locale = tagOf(locale);

  /// The language a key is looked up in last.
  final String fallback;

  String _locale;
  final Map<String, Map<String, String>> _tables = {};

  /// The language in use.
  String get locale => _locale;

  /// The languages that have a table, in alphabetical order.
  List<String> get locales => _tables.keys.toList()..sort();

  /// [tag] as the tables are keyed: lower case, with a hyphen, so `fr_CA`,
  /// `fr-CA` and `fr-ca` are one language. Throws [ArgumentError] when empty.
  static String tagOf(String tag) {
    final clean = tag.trim().toLowerCase().replaceAll('_', '-');
    if (clean.isEmpty) throw ArgumentError.value(tag, 'tag', 'is empty');
    return clean;
  }

  /// The plural form [count] takes in [locale]: one of zero, one, two, few,
  /// many and other.
  ///
  /// shortcut: whole counts only, and the rules are the ones for the
  /// languages listed in this file. Add a language here when a game ships it.
  static String pluralForm(String locale, int count) {
    final language = tagOf(locale).split('-').first;
    return (_rules[language] ?? _oneOrOther)(count.abs());
  }

  /// Switches the language, and tells listeners when it changed.
  void use(String locale) {
    final tag = tagOf(locale);
    if (tag == _locale) return;
    _locale = tag;
    notifyListeners();
  }

  /// Adds [table] to the keys [locale] has, replacing any it already had.
  void add(String locale, Map<String, String> table) {
    _tables.putIfAbsent(tagOf(locale), () => {}).addAll(table);
    notifyListeners();
  }

  /// Adds the table in [source], one JSON object of key to text, which is
  /// what a language file holds. Throws [FormatException] for anything else.
  void addJson(String locale, String source) {
    final parsed = jsonDecode(source);
    if (parsed is! Map || parsed.values.any((text) => text is! String)) {
      throw FormatException('A language file is one object of text', source);
    }
    add(locale, parsed.cast<String, String>());
  }

  /// The languages looked in for [locale], first to last: the language
  /// itself, its parent without the region, and then the default.
  List<String> chain(String locale) {
    final tag = tagOf(locale);
    return {tag, tag.split('-').first, fallback}.toList();
  }

  /// The text for [key] in the language in use, with each `{name}` in it
  /// replaced by the argument of that name.
  ///
  /// A whole `count` argument picks the plural form: the table holds
  /// `key.one`, `key.other` and so on, and `key` itself is the last resort.
  /// A name with no argument is left as written, which is why a text needs no
  /// way to write a literal brace.
  String text(String key, [Map<String, Object?> args = const {}]) {
    final found = _find(key, args['count']);
    if (found == null) return key;
    return found.replaceAllMapped(
      RegExp(r'\{(\w+)\}'),
      (match) => args.containsKey(match[1]) ? '${args[match[1]]}' : match[0]!,
    );
  }

  String? _find(String key, Object? count) {
    for (final tag in chain(_locale)) {
      final forms = [
        if (count is int) ...['$key.${pluralForm(tag, count)}', '$key.other'],
        key,
      ];
      final table = _tables[tag];
      final hit = table == null
          ? null
          : forms.map((form) => table[form]).nonNulls.firstOrNull;
      if (hit != null) return hit;
    }
    return null;
  }

  static final _formSuffix = RegExp(r'\.(zero|one|two|few|many|other)$');

  /// The keys each language has no text for, for a translator's to-do list.
  ///
  /// A key counts as known when any table has it or [used] names it. A
  /// language lacks it when neither its own table nor its parent language's
  /// has it, so `fr-ca` is not asked for what `fr` says. The default is
  /// checked as well, which is where a key used but written nowhere shows.
  /// Plural forms are one key, named without their last part.
  ///
  /// shortcut: a language with one of a key's forms counts as having it,
  /// whether or not it has every form it needs.
  Map<String, List<String>> gaps([Iterable<String> used = const []]) {
    String stem(String key) => key.replaceFirst(_formSuffix, '');
    final every = {
      for (final table in _tables.values) ...table.keys.map(stem),
      ...used,
    };
    final out = <String, List<String>>{};
    for (final locale in {...locales, fallback}) {
      final own = {
        for (final tag in {locale, locale.split('-').first})
          ...?_tables[tag]?.keys.map(stem),
      };
      final lacking = every.difference(own).toList()..sort();
      if (lacking.isNotEmpty) out[locale] = lacking;
    }
    return out;
  }
}
