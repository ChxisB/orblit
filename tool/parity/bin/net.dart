import 'dart:async';
import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/net/range_queue.dart';
import 'package:orblit_parity_tool/fixture.dart';

Future<Map<String, Object?>> _case(
  String op,
  List<Object?> input,
  FutureOr<Object?> Function() body,
) async {
  try {
    return {
      'op': op,
      'in': input,
      'expect': {'value': await body()},
    };
  } on Object {
    return {
      'op': op,
      'in': input,
      'expect': {'throws': true},
    };
  }
}

String _hashOf(String text) => ContentHash.of(utf8.encode(text)).hex;

// Origins. A refusal is part of the answer, so the reason travels with it.

Map<String, Object?> _opts({
  List<String> hosts = const [],
  bool http = false,
}) => {'hosts': hosts, 'insecure': http};

Object? _origin(
  String base,
  Map<String, Object?> opts,
  Object? Function(AssetOrigin) use,
) {
  try {
    return use(
      AssetOrigin.parse(
        base,
        policy: FetchPolicy(
          hosts: {...(opts['hosts']! as List).cast<String>()},
          allowInsecure: opts['insecure']! as bool,
        ),
      ),
    );
  } on FetchRefused catch (refused) {
    return {'refused': refused.reason};
  }
}

const _bases = <String>[
  'https://assets.example.com/game/v3',
  'https://assets.example.com/game/v3/',
  'https://e.example.com',
  'https://e.example.com/',
  'https://E.Example.COM/A',
  'HTTPS://e.example.com/a',
  'https://e.example.com:443/a',
  'https://e.example.com:8443/a',
  'https://e.example.com:80/a',
  'http://localhost:8080/assets',
  'http://e.example.com/a',
  'file:///etc/passwd',
  '/just/a/path',
  'ftp://e.example.com/a',
  '//e.example.com/a',
  '',
  'https://',
  'https:///a',
  'https://e.example.com/a?x=1',
  'https://e.example.com/a#frag',
  'https://user:pw@e.example.com/a',
  'https://e.example.com/a b',
  'https://e.example.com/%61/',
  'https://e.example.com/a/../b',
  'https://e.example.com/a/./b/',
  'https://e.example.com/ü/',
  'https://münchen.example/a',
  'https://[::1]/a',
  'https://e.example.com/a%2Fb',
  'not a url',
  'mailto:x@y.example',
  'data:text/plain,hi',
  'javascript:alert(1)',
  'https://e.example.com//a',
  'https://e.example.com/a//',
  'https://e.example.com./a',
];

const _ids = <String>[
  'models/robot.glb',
  'a',
  't/metal plate#2.png',
  'a b#c?d.png',
  'ünï/çödé.png',
  "a!b*c'd(e).png",
  'a%b.png',
  'a%41.png',
  'a+b.png',
  'a~b_c-d.png',
  'a/b/c',
  'a:b',
  'a@b',
  'a&b=c',
  r'a$b',
  'a,b;c',
  'a[b]',
  'a{b}',
  'a|b',
  'a<b>',
  'a"b',
  'a^b',
  'a`b',
  '😀/x.png',
  'models/../x',
  '',
];

const _modelFolder = '/models/robot.gltf';

const _refs = <String>[
  '../textures/metal.png',
  './x.png',
  'x.png',
  '/abs/x.png',
  '/game/v3/x.png',
  '//evil.example.net/x.png',
  '//assets.example.com/game/v3/x.png',
  'https://assets.example.com/game/v3/a.png',
  'HTTPS://ASSETS.EXAMPLE.COM/game/v3/a.png',
  'https://cdn.example.com/x.png',
  'https://CDN.example.com/anywhere/x.png',
  'https://evil.cdn.example.com/x.png',
  'https://cdn.example.com.evil.net/x.png',
  'file:///etc/passwd',
  'data:image/png;base64,AAAA',
  'x.png?v=2',
  'x.png#frag',
  'x%20y.png',
  'x y.png',
  '%2e%2e/x.png',
  '..%2fx.png',
  '../../x.png',
  '../../../../../../x.png',
  '../../../secrets/keys.json',
  '',
  '.',
  '..',
  '#frag',
  '?q',
  'http://assets.example.com/game/v3/x.png',
  'https://assets.example.com:443/game/v3/x.png',
  'https://assets.example.com:8443/game/v3/x.png',
  'https://assets.example.com/game/v30/x.png',
  'https://assets.example.com/game/v3',
  'https://user@assets.example.com/game/v3/x.png',
  'https://assets.example.com./game/v3/x.png',
  'javascript:alert(1)',
  'blob:x',
  r'\\evil\x',
  'a:b.png',
  './a:b.png',
  '1:b',
  'x/../../x.png',
  'x/./y/../z.png',
  'ü.png',
  'x.png%',
  'x.png%zz',
];

const _allowUrls = <String>[
  'https://assets.example.com/game/v3/x.png',
  'https://assets.example.com/elsewhere/x.png',
  'https://ASSETS.example.com/x.png',
  'https://cdn.example.com/x.png',
  'https://CDN.EXAMPLE.COM/x.png',
  'https://evil.cdn.example.com/x.png',
  'https://cdn.example.com.evil.net/x.png',
  'http://assets.example.com/x.png',
  'http://cdn.example.com/x.png',
  'http://localhost:8080/x.png',
  'file:///etc/passwd',
  'ftp://assets.example.com/x.png',
  '/just/a/path',
  'x.png',
  '',
  'https:///x.png',
  'https://assets.example.com:8443/x.png',
  'https://user@assets.example.com/x.png',
  'https://localhost/x.png',
];

final _policies = <Map<String, Object?>>[
  _opts(),
  _opts(hosts: ['cdn.example.com']),
  _opts(hosts: ['CDN.Example.com', 'localhost'], http: true),
  _opts(http: true),
];

Future<List<Map<String, Object?>>> _originCases() async => [
  for (final base in _bases)
    for (final opts in [_opts(), _opts(http: true)])
      await _case(
        'origin.parse',
        [base, opts],
        () => _origin(
          base,
          opts,
          (o) => {
            'base': o.base.toString(),
            'host': o.base.host,
            'path': o.base.path,
          },
        ),
      ),
  for (final base in [
    'https://assets.example.com/game/v3',
    'https://e.example.com',
    'https://E.Example.COM/A',
    'https://e.example.com:8443/a',
    'https://e.example.com/ü/',
    'https://e.example.com/a%2Fb',
    'https://e.example.com/a b',
    'https://e.example.com/a/../b',
  ])
    for (final id in _ids)
      await _case(
        'origin.url',
        [base, _opts(), id],
        () => _origin(
          base,
          _opts(),
          (o) => {'url': o.urlOf(AssetId.parse(id)).toString()},
        ),
      ),
  for (final opts in _policies)
    for (final ref in _refs)
      await _case(
        'origin.beside',
        [
          'https://assets.example.com/game/v3',
          opts,
          'https://assets.example.com/game/v3$_modelFolder',
          ref,
        ],
        () => _origin(
          'https://assets.example.com/game/v3',
          opts,
          (o) => {
            'url': o
                .beside(
                  Uri.parse('https://assets.example.com/game/v3$_modelFolder'),
                  ref,
                )
                .toString(),
          },
        ),
      ),
  for (final from in [
    'https://cdn.example.com/models/robot.gltf',
    'https://assets.example.com/game/v3/a/b/c.gltf',
    'https://assets.example.com/game/v3/a/b/',
    'https://assets.example.com/game/v3/a?x=1',
    'https://assets.example.com/game/v3/a#frag',
  ])
    for (final ref in [
      '../x.png',
      'x.png',
      '/game/v3/x.png',
      '../../../x.png',
      '?q',
      '#f',
      '',
    ])
      await _case(
        'origin.beside',
        [
          'https://assets.example.com/game/v3',
          _opts(hosts: ['cdn.example.com']),
          from,
          ref,
        ],
        () => _origin(
          'https://assets.example.com/game/v3',
          _opts(hosts: ['cdn.example.com']),
          (o) => {'url': o.beside(Uri.parse(from), ref).toString()},
        ),
      ),
  for (final opts in _policies)
    for (final url in _allowUrls)
      await _case(
        'origin.allows',
        ['https://assets.example.com/game/v3', opts, url],
        () => _origin('https://assets.example.com/game/v3', opts, (o) {
          final parsed = Uri.parse(url);
          return {'allows': o.allows(parsed)};
        }),
      ),
  for (final opts in _policies)
    for (final url in _allowUrls)
      await _case(
        'origin.require',
        ['https://assets.example.com/game/v3', opts, url],
        () => _origin('https://assets.example.com/game/v3', opts, (o) {
          o.requireAllowed(Uri.parse(url));
          return {'allowed': true};
        }),
      ),
];

// Urls. The origin rules lean on how Uri reads, joins and tidies an address,
// so those are pinned on their own.

const _urlTexts = <String>[
  'https://e.example.com/a/b?x=1#f',
  'ftp://e.example.com:0/a',
  'https://e.example.com:0/a',
  '//e.example.com:0/a',
  'a/..',
  'a/../',
  'a/b/../..',
  './',
  '../',
  'a/.',
  'urn:a/./b',
  'a%3Ab',
  'a%40b',
  'a%21b',
  'a%27b',
  'https://e%zz.example/a',
  'https://e.example.com%2Fa/b',
  'https://e.example.com/a%2e%2e',
  'https://[2001:db8::1]:443/a',
  'https://[::1]:/a',
  'https://[]/a',
  'https://[::1]x/a',
  'https://e.example.com:80:80/a',
  'https://e.example.com:8x/a',
  'https://user:pw@[::1]:81/a',
  'https://%61:%62@e.example.com/',
  'https://e.example.com/?a=b&c=d',
  'https://e.example.com/a?b#c',
  'HTTPS://E.EXAMPLE.COM:443/a',
  'http://e.example.com:80/a',
  'http://e.example.com:8080/a',
  'http://e.example.com:443/a',
  'https://e.example.com:80/a',
  'https://e.example.com:/a',
  'https://e.example.com:abc/a',
  'https://e.example.com:65535/a',
  'https://e.example.com:0443/a',
  'ftp://e.example.com:21/a',
  'ws://e.example.com:80/a',
  'https://user@e.example.com/a',
  'https://user:pw@e.example.com/a',
  'https://:pw@e.example.com/a',
  'https://us%65r@e.example.com/a',
  'https://us er@e.example.com/a',
  'https://a@b@c/',
  'https://[::1]/a',
  'https://[::1]:8080/a',
  'https://[::ffff:1.2.3.4]/a',
  'https://[2001:DB8::1]/a',
  'https://[::1/a',
  'https://[zz]/a',
  'https://1.2.3.4/a',
  'https://e.example.com',
  'https://e.example.com?x',
  'https://e.example.com#f',
  'https://e.example.com?',
  'https://e.example.com/a?',
  'https://e.example.com/a#',
  'http://e.example.com',
  'ftp://e.example.com',
  'file:///etc/passwd',
  'file://host/x',
  'mailto:x@y.example',
  'data:text/plain,hi',
  'urn:isbn:123',
  'javascript:alert(1)',
  'blob:https://e.example.com/x',
  '//e.example.com/a',
  '//e.example.com',
  '/a/b',
  'a/b',
  '../a',
  './a',
  '',
  '#f',
  '?q',
  '.',
  '..',
  'a:b',
  'a:',
  '1:b',
  ':b',
  'a+b.c-d:e',
  'a_b:c',
  'a b',
  'a%',
  'a%z',
  'a%2',
  'a%41',
  'a%7e',
  'a%7E',
  'a%2f',
  'a%2F',
  'a%c3%bc',
  'a%C3%BC',
  'a%00',
  'ü',
  'a/ü',
  'a?ü',
  'a#ü',
  'a|b',
  'a^b',
  'a`b',
  'a"b',
  'a<b>',
  'a{b}',
  r'a\b',
  "a'b",
  'a[b]',
  'a?b[c]',
  'a?b c',
  'a#b c',
  'a#b#c',
  'a?b?c',
  'a?b#c?d',
  'a;b=c',
  'a,b',
  'a@b',
  'a&b=c',
  r'a$b',
  'a!b*c(d)',
  'a+b',
  'a~b_c-d.e',
  '😀',
  'https://e.example.com/a/../b',
  '/a/./b/../c/.',
  '/..',
  '/../a',
  '/.',
  '/a/..',
  '/a/../',
  'a/../../b',
  'a/b/..',
  'a/b/../',
  'a/./b',
  './../a',
  '../../a',
  '/a//b',
  'https://e.example.com//a',
  'https://e.example.com/a/%2e%2e/b',
  'https://e.example.com/a/%2E%2E/b',
  'https://e.example.com/a/%2e/b',
  '/%2E/a',
  '/a/%2e',
  'https://e.example.com./a',
  'https://e.example.com../a',
  'https://E.Example.COM./a',
  'https://münchen.example/a',
  'https://MÜNCHEN.example/a',
  'https://e%78ample.com/a',
  'https://E%58ample.com/a',
  r'https://e.example.com\a',
  r'https://e.example.com\@evil.net/',
  'HTTP://x',
  'hTtPs://x',
  'https:/e.example.com/a',
  'https:e.example.com/a',
  'https:///a',
  'https://',
  'https://?x',
  'https://#f',
  'https://:80',
  'https://@/a',
  '  https://e.example.com/a',
  'https://e.example.com/a  ',
  'https://e.example.com/a\n',
  'https://e.example.com/a\tb',
  'https://e.example.com/\u0000',
  'https://e.example.com/a\u007f',
  'https://e.example.com/a\u0080',
  'https://e.example.com/ a',
  'https://exa mple.com/a',
  'https://e.example.com/a%20b',
  'https://e.example.com/a b%20c',
  'https://e.example.com/%',
  'https://e.example.com/%zz',
  'https://e.example.com/a?b=%',
  'https://e.example.com/a?b=%41&c=%zz',
  'https://e.example.com/a#%41',
  'https://e.example.com/a#%',
  'https://e.example.com/%e9',
  'https://e.example.com/%ff',
  'https://e.example.com/%C0%80',
  'https://e.example.com/%ed%a0%80',
  'a?b\\c',
  'a#b\\c',
  'https://e.example.com/?x=\\y',
  'a b:c',
  'https://a[b]c/',
  './a:b',
  './a:b:c',
  'x/../a:b',
  '../a:b',
  './x/a:b',
  './/a:b',
  './a:b/c:d',
  '/./a:b',
  '/a:b',
  'x/..//a:b',
  './:b',
  './a:',
  '././a:b',
  'x/./a:b',
  'x/../../a:b',
  'a/b/../../c:d',
  './a:b?q:r',
  './a:b#f:g',
  '..',
  './a%3Ab',
  './.a:b',
  './a.:b',
  'https://a]b/',
  'https://a|b/',
  'https://a"b/',
  'https://a<b/',
  '//',
  '///',
  '/a/b/..//c',
  'a/..//b',
  'a//..',
  '//e.example.com/a/b/..//c',
  'a/./',
  './/a',
  'a/../..',
  'a/b/../../..',
  '/a/b/../../..',
  'https://e.example.com/a;b/../c',
  'https://e.example.com/a/b/%2E%2e/c',
  'https://e.example.com/a?b=%7e%2f%3A',
  'https://e.example.com/a?b=\x7f',
  'https://e.example.com/a#b=\x7f',
  'https://us:er:x@e.example.com/',
  'https://us/er@e.example.com/',
  'https://us?er@e.example.com/',
  'https://us[er@e.example.com/',
  'https://@e.example.com/',
  'https://:@e.example.com/',
  'https://e.example.com:80@x/',
  'https://[::1]@e.example.com/',
  'https://a b@e.example.com/',
  'https://ü@e.example.com/',
  'https://[1:2:3:4:5:6:7:8]/a',
  'https://[1:2:3:4:5:6:7:8:9]/a',
  'https://[1::2::3]/a',
  'https://[:::1]/a',
  'https://[::1.2.3.4]/a',
  'https://[::1.2.3]/a',
  'https://[1:2:3:4:5:6:1.2.3.4]/a',
  'https://[12345::1]/a',
  'https://[g::1]/a',
  'https://[::1][::2]/a',
  'https://[::1]:80:90/a',
];

const _resolveBases = <String>[
  'https://a.example/b/c/d;p?q',
  'https://a.example/b/c/d',
  'https://a.example',
  'https://a.example/',
  'https://a.example/b/c/',
  'https://u@a.example:8443/b/c?q#f',
  'https://a.example/b/c/d?q#f',
  'https://a.example/b%20c/d e',
];

const _resolveRefs = <String>[
  'g:h',
  'g',
  './g',
  'g/',
  '/g',
  '//g',
  '?y',
  'g?y',
  '#s',
  'g#s',
  'g?y#s',
  ';x',
  'g;x',
  'g;x?y#s',
  '',
  '.',
  './',
  '..',
  '../',
  '../g',
  '../..',
  '../../',
  '../../g',
  '../../../g',
  '../../../../g',
  '/./g',
  '/../g',
  'g.',
  '.g',
  'g..',
  '..g',
  './../g',
  './g/.',
  'g/./h',
  'g/../h',
  'g;x=1/./y',
  'g;x=1/../y',
  'g?y/./x',
  'g?y/../x',
  'g#s/./x',
  'g#s/../x',
  'http:g',
  'https:g',
  'https://other.example/x',
  'HTTPS://OTHER.EXAMPLE:443/x',
  'ü',
  '%2e%2e/g',
  'g%2fh',
  'g h',
  '//g/../h',
  '///g',
  '//u@g:80/x',
  'a:b',
  'x/../../..',
];

final _encodeTexts = <String>[
  '',
  'abc',
  'ABCxyz019',
  '-_.!~*()\'',
  'a b',
  'a/b',
  'a?b#c',
  'a%b',
  'a+b',
  'a&b=c',
  r'a$b,c;d:e@f',
  'a[b]{c}|d^e`f"g<h>i\\j',
  'ü',
  'é',
  '€',
  '😀',
  'a😀b',
  '\u0000\u0001\u007f\u0080',
  'ÿĀ߿ࠀ￿',
  '😀',
  ' ',
];

Map<String, Object?> _urlOut(Uri u) => {
  'scheme': u.scheme,
  'hasAuthority': u.hasAuthority,
  'userInfo': u.userInfo,
  'host': u.host,
  'port': u.hasPort ? u.port : -1,
  'path': u.path,
  'hasQuery': u.hasQuery,
  'query': u.query,
  'hasFragment': u.hasFragment,
  'fragment': u.fragment,
  'text': u.toString(),
};

Future<List<Map<String, Object?>>> _urlCases() async => [
  for (final text in _urlTexts)
    await _case('url.parse', [text], () => _urlOut(Uri.parse(text))),
  for (final base in _resolveBases)
    for (final ref in _resolveRefs)
      await _case('url.resolve', [
        base,
        ref,
      ], () => _urlOut(Uri.parse(base).resolveUri(Uri.parse(ref)))),
  for (final text in _encodeTexts)
    await _case('url.encode', [text], () => Uri.encodeComponent(text)),
];

// Records.

Map<String, Object?> _recordOut(FetchRecord? record) => record == null
    ? const {}
    : {
        'hash': record.hash.hex,
        'bytes': big(record.bytes),
        'etag': record.etag,
        'fetched': record.fetched?.toUtc().toIso8601String(),
        'micros': record.fetched == null
            ? null
            : big(record.fetched!.microsecondsSinceEpoch),
        'utc': record.fetched?.isUtc,
      };

final _h = _hashOf('one');

// Texts for FetchRecord.fromJson. A time with no zone reads as local time in
// Dart, which depends on the machine, so every time here names its zone.
final _recordTexts = <String>[
  '{"hash":"$_h","bytes":12}',
  '{"hash":"$_h","bytes":0}',
  '{"hash":"$_h","bytes":12,"etag":"\\"abc\\""}',
  '{"hash":"$_h","bytes":12,"etag":null}',
  '{"hash":"$_h","bytes":12,"etag":5}',
  '{"hash":"$_h","bytes":12,"etag":""}',
  '{"hash":"${_h.toUpperCase()}","bytes":12}',
  '{"hash":"${_h.substring(1)}","bytes":12}',
  '{"hash":"${'z' * 64}","bytes":12}',
  '{"hash":5,"bytes":12}',
  '{"hash":null,"bytes":12}',
  '{"bytes":12}',
  '{"hash":"$_h"}',
  '{"hash":"$_h","bytes":-1}',
  '{"hash":"$_h","bytes":12.0}',
  '{"hash":"$_h","bytes":1e3}',
  '{"hash":"$_h","bytes":-0}',
  '{"hash":"$_h","bytes":"12"}',
  '{"hash":"$_h","bytes":null}',
  '{"hash":"$_h","bytes":9223372036854775807}',
  '{"hash":"$_h","bytes":9223372036854775808}',
  '{"hash":"$_h","bytes":18446744073709551616}',
  '{"hash":"$_h","bytes":12,"extra":[1,2]}',
  '[]',
  'null',
  '"x"',
  '5',
  '{}',
  '{"hash":"$_h","bytes":12,"fetched":5}',
  '{"hash":"$_h","bytes":12,"fetched":null}',
  '{"hash":"$_h","bytes":12,"fetched":""}',
  '{"hash":"$_h","bytes":12,"fetched":"yesterday"}',
  '{"hash":"$_h","bytes":12,"fetched":[]}',
  for (final time in [
    '2026-10-21T10:00:00.123Z',
    '2026-10-21T10:00:00Z',
    '2026-10-21T10:00:00.123456Z',
    '2026-10-21T10:00:00.1Z',
    '2026-10-21T10:00:00.12Z',
    '2026-10-21T10:00:00.1234Z',
    '2026-10-21T10:00:00.12345Z',
    '2026-10-21T10:00:00.1234567Z',
    '2026-10-21T10:00:00.9999999Z',
    '2026-10-21T10:00:00.000001Z',
    '2026-10-21T10:00:00.001Z',
    '2026-10-21T10:00:00,5Z',
    '2026-10-21T10:00Z',
    '2026-10-21T10Z',
    '2026-10-21t10:00:00z',
    '2026-10-21 10:00:00Z',
    '2026-10-21 10:00:00 Z',
    '20261021T100000Z',
    '20261021T1000Z',
    '2026-1021T10:00:00Z',
    '202610-21T10:00:00Z',
    '2026-10-21T10:00:00+01:00',
    '2026-10-21T10:00:00-05:30',
    '2026-10-21T10:00:00+0100',
    '2026-10-21T10:00:00+01',
    '2026-10-21T10:00:00 +01:00',
    '2026-10-21T23:30:00-02:00',
    '2026-10-21T10:00:00.123+01:00',
    '2026-12-31T23:59:59Z',
    '2026-02-30T10:00:00Z',
    '2026-02-29T10:00:00Z',
    '2024-02-29T10:00:00Z',
    '2026-13-01T10:00:00Z',
    '2026-00-10T10:00:00Z',
    '2026-10-00T10:00:00Z',
    '2026-10-32T10:00:00Z',
    '2026-10-21T24:00:00Z',
    '2026-10-21T25:61:61Z',
    '2026-10-21T10:60:00Z',
    '2026-10-21T10:00:60Z',
    '2026-10-21T99:99:99Z',
    '1970-01-01T00:00:00Z',
    '1969-12-31T23:59:59.999999Z',
    '0001-01-01T00:00:00Z',
    '9999-12-31T23:59:59.999999Z',
    '+002026-10-21T10:00:00Z',
    '2026-10-21T10:00:00Zjunk',
    ' 2026-10-21T10:00:00Z',
    '2026-10-21T10:00:00Z ',
    '2026-10-21T1:00:00Z',
    '2026-10-21T10:00:00.Z',
    '２０２６-10-21T10:00:00Z',
    '2026-10-21T10:00:00+24:00',
    '2026-10-21T10:00:00+99:99',
    '2026-10-21T10:00:00.123456789012345678Z',
  ])
    '{"hash":"$_h","bytes":12,"fetched":${jsonEncode(time)}}',
];

Map<Uri, FetchRecord> _recordsOf(List<Object?> spec) => {
  for (final raw in spec)
    Uri.parse((raw! as List)[0]! as String): _record(
      (raw as List)[1]! as Map<String, Object?>,
    ),
};

FetchRecord _record(Map<String, Object?> spec) => FetchRecord(
  hash: ContentHash.parse(spec['hash']! as String),
  bytes: spec['bytes']! as int,
  etag: spec['etag'] as String?,
  fetched: spec['fetched'] == null
      ? null
      : DateTime.parse(spec['fetched']! as String),
);

Map<String, Object?> _spec(
  String name, {
  int bytes = 1,
  String? etag,
  String? fetched,
}) => {
  'hash': _hashOf(name),
  'bytes': bytes,
  'etag': ?etag,
  'fetched': ?fetched,
};

final _encodes = <List<Object?>>[
  [],
  [
    ['https://e.example.com/a.png', _spec('a')],
  ],
  [
    ['https://e.example.com/b.png', _spec('b', etag: '"x"')],
    [
      'https://e.example.com/a.png',
      _spec('a', fetched: '2026-10-21T10:00:00Z'),
    ],
  ],
  [
    ['https://e.example.com/B.png', _spec('b')],
    ['https://e.example.com/a.png', _spec('a')],
    ['https://e.example.com/Z.png', _spec('z')],
    ['https://e.example.com/_.png', _spec('_')],
    ['https://e.example.com/ü.png', _spec('u')],
    ['https://e.example.com/a.png?x=1', _spec('q')],
    ['https://e.example.com/a.png#f', _spec('f')],
    ['https://e.example.com:8443/a.png', _spec('p')],
    ['http://e.example.com/a.png', _spec('h')],
    ['https://a.example.com/z.png', _spec('d')],
  ],
  [
    [
      'https://e.example.com/a.png',
      _spec('a', etag: 'quote"and\\slash\u00e9\u{1f600}', bytes: 1 << 40),
    ],
    [
      'https://e.example.com/b.png',
      _spec('b', fetched: '2026-10-21T10:00:00.123456Z'),
    ],
    [
      'https://e.example.com/c.png',
      _spec('c', fetched: '2026-10-21T10:00:00.5+02:00'),
    ],
    ['https://e.example.com/d.png', _spec('d', bytes: 0, etag: '')],
  ],
];

final _decodes = <String>[
  '',
  'not json',
  '[]',
  'null',
  '{}',
  '{"formatVersion":1}',
  '{"formatVersion":1,"records":[]}',
  '{"formatVersion":1,"records":{}}',
  '{"formatVersion":2,"records":{}}',
  '{"formatVersion":"1","records":{}}',
  '{"formatVersion":1.0,"records":{}}',
  '{"records":{}}',
  for (final spec in _encodes) FetchRecordsCodec.encode(_recordsOf(spec)),
  '{"formatVersion":1,"records":{"https://e.example.com/a.png":{"hash":"$_h","bytes":1},"https://e.example.com/b.png":5,"https://e.example.com/c.png":{"hash":"x","bytes":1},"https://e.example.com/d.png":{"hash":"$_h","bytes":-1},"https://e.example.com/e.png":{"hash":"$_h","bytes":2}}}',
  '{"formatVersion":1,"records":{"/just/a/path":{"hash":"$_h","bytes":1}}}',
  '{"formatVersion":1,"records":{"":{"hash":"$_h","bytes":1}}}',
  '{"formatVersion":1,"records":{"not a url":{"hash":"$_h","bytes":1}}}',
  '{"formatVersion":1,"records":{"HTTPS://E.EXAMPLE.COM/A.png":{"hash":"$_h","bytes":1}}}',
  '{"formatVersion":1,"records":{"https://e.example.com/a.png":{"hash":"$_h","bytes":1},"https://e.example.com/a.png":{"hash":"$_h","bytes":2}}}',
  '{"formatVersion":1,"records":{"https://e.example.com:443/a.png":{"hash":"$_h","bytes":1},"https://e.example.com/a.png":{"hash":"$_h","bytes":2}}}',
  '{"formatVersion":1,"records":{"https://e.example.com/a b.png":{"hash":"$_h","bytes":1}}}',
  '{"formatVersion":1,"records":{"https://e.example.com/a%20b.png":{"hash":"$_h","bytes":1}}}',
  '{"formatVersion":1,"records":{"https://e.example.com/ü.png":{"hash":"$_h","bytes":1}}}',
  '{"formatVersion":1,"records":{"https://e.example.com/a.png":{"hash":"$_h","bytes":1,"fetched":"2026-10-21T10:00:00Z"}},"other":1}',
];

Future<List<Map<String, Object?>>> _recordCases() async => [
  for (final text in _recordTexts)
    await _case('record.parse', [
      text,
    ], () => _recordOut(FetchRecord.fromJson(jsonDecode(text)))),
  for (final spec in _encodes)
    await _case('records.encode', [
      spec,
    ], () => FetchRecordsCodec.encode(_recordsOf(spec))),
  for (final text in _decodes)
    await _case(
      'records.decode',
      [text],
      () => [
        for (final MapEntry(:key, :value) in FetchRecordsCodec.decode(
          text,
        ).entries)
          [key.toString(), _recordOut(value)],
      ],
    ),
  for (final text in _decodes)
    await _case('records.reencode', [
      text,
    ], () => FetchRecordsCodec.encode(FetchRecordsCodec.decode(text))),
];

// Picture sizes.

Uint8List _png(int width, int height, {int length = 64}) {
  final bytes = Uint8List(length);
  bytes.setAll(0, const [137, 80, 78, 71, 13, 10, 26, 10]);
  final view = ByteData.view(bytes.buffer);
  if (length >= 12) view.setUint32(8, 13);
  if (length >= 16) bytes.setAll(12, utf8.encode('IHDR'));
  if (length >= 20) view.setUint32(16, width);
  if (length >= 24) view.setUint32(20, height);
  return bytes;
}

Uint8List _jpeg(
  int width,
  int height, {
  int before = 0,
  int frame = 0xc0,
  List<int> fill = const [],
  List<int> standalone = const [],
}) {
  final out = BytesBuilder()..add([0xff, 0xd8]);
  for (var at = 0; at < before; at++) {
    out.add([0xff, 0xe0, 0x00, 0x10]);
    out.add(List.filled(14, 0));
  }
  out.add(standalone);
  out.add(fill);
  out.add([
    0xff,
    frame,
    0x00,
    0x11,
    0x08,
    (height >> 8) & 0xff,
    height & 0xff,
    (width >> 8) & 0xff,
    width & 0xff,
  ]);
  out.add([0xff, 0xda]);
  return out.toBytes();
}

Uint8List _webp(String tag, List<int> payload, {int pad = 0}) {
  final out = BytesBuilder()
    ..add(utf8.encode('RIFF'))
    ..add([0, 0, 0, 0])
    ..add(utf8.encode('WEBP'))
    ..add(utf8.encode(tag))
    ..add([0, 0, 0, 0])
    ..add(payload)
    ..add(List.filled(pad, 0));
  return out.toBytes();
}

Uint8List _webpLossy(int width, int height) => _webp('VP8 ', [
  0, 0, 0, 0x9d, 0x01, 0x2a, //
  width & 0xff, (width >> 8) & 0xff, height & 0xff, (height >> 8) & 0xff,
]);

Uint8List _webpLossless(int width, int height) {
  final packed = (width - 1) | ((height - 1) << 14);
  return _webp('VP8L', [
    0x2f,
    packed & 0xff,
    (packed >> 8) & 0xff,
    (packed >> 16) & 0xff,
    (packed >> 24) & 0xff,
  ]);
}

Uint8List _webpExtended(int width, int height) => _webp('VP8X', [
  0, 0, 0, 0, //
  (width - 1) & 0xff, ((width - 1) >> 8) & 0xff, ((width - 1) >> 16) & 0xff,
  (height - 1) & 0xff, ((height - 1) >> 8) & 0xff, ((height - 1) >> 16) & 0xff,
]);

Uint8List _ktx2Header(
  int width,
  int height, {
  int depth = 0,
  int layers = 0,
  int faces = 1,
  int levels = 1,
  int length = 80,
}) {
  final bytes = Uint8List(length);
  bytes.setAll(0, const [
    0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A, //
  ]);
  final view = ByteData.view(bytes.buffer);
  if (length >= 24) view.setUint32(20, width, Endian.little);
  if (length >= 28) view.setUint32(24, height, Endian.little);
  if (length >= 32) view.setUint32(28, depth, Endian.little);
  if (length >= 36) view.setUint32(32, layers, Endian.little);
  if (length >= 40) view.setUint32(36, faces, Endian.little);
  if (length >= 44) view.setUint32(40, levels, Endian.little);
  return bytes;
}

final _pictures = <Uint8List>[
  _png(1920, 1080),
  _png(1, 1),
  _png(0, 5),
  _png(5, 0),
  _png(0xFFFFFFFF, 0xFFFFFFFF),
  _png(0x7FFFFFFF, 2),
  _png(64000, 64000),
  _png(1024, 1024, length: 18),
  _png(1024, 1024, length: 24),
  _png(1024, 1024, length: 23),
  _jpeg(800, 600),
  _jpeg(800, 600, before: 2),
  _jpeg(640, 480, frame: 0xc2),
  _jpeg(640, 480, frame: 0xc1),
  _jpeg(640, 480, frame: 0xc3),
  _jpeg(640, 480, frame: 0xc5),
  _jpeg(640, 480, frame: 0xcf),
  _jpeg(640, 480, frame: 0xc4),
  _jpeg(640, 480, frame: 0xc8),
  _jpeg(640, 480, frame: 0xcc),
  _jpeg(640, 480, frame: 0xcd),
  _jpeg(640, 480, fill: [0xff, 0xff, 0xff]),
  _jpeg(640, 480, standalone: [0xff, 0xd0, 0xff, 0x01, 0xff, 0xd9]),
  _jpeg(0, 480),
  _jpeg(640, 0),
  _jpeg(65535, 65535),
  Uint8List.fromList([0xff, 0xd8]),
  Uint8List.fromList([0xff, 0xd8, 0xff]),
  Uint8List.fromList([0xff, 0xd8, 0xff, 0xe0]),
  Uint8List.fromList([0xff, 0xd8, 0xff, 0xe0, 0x00, 0x01, 0, 0]),
  Uint8List.fromList([0xff, 0xd8, 0xff, 0xda, 0x00, 0x08, 0, 0, 0, 0]),
  Uint8List.fromList([0xff, 0xd8, 0x00, 0x00, 0x00, 0x00]),
  Uint8List.fromList([0xff, 0xd8, 0xff, 0xc0, 0x00, 0x11, 0x08, 0, 1, 0]),
  _webpLossy(1254, 1254),
  _webpLossy(16383, 16383),
  _webpLossy(0, 0),
  _webpLossless(1254, 1254),
  _webpLossless(16384, 16384),
  _webpLossless(1, 1),
  _webpExtended(1254, 1254),
  _webpExtended(16777216, 16777216),
  _webpExtended(1, 1),
  _webp('VP8 ', [0, 0, 0, 0x9d, 0x01, 0x2b, 1, 0, 1, 0]),
  _webp('VP8L', [0x2e, 0, 0, 0, 0]),
  _webp('ABCD', [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
  Uint8List.fromList(utf8.encode('RIFF\u0000\u0000\u0000\u0000WEBP')),
  Uint8List.fromList(utf8.encode('RIFF\u0000\u0000\u0000\u0000WAVEfmt ')),
  _ktx2Header(2048, 2048),
  _ktx2Header(256, 256, levels: 9),
  _ktx2Header(512, 512, faces: 6),
  _ktx2Header(64, 64, layers: 4),
  _ktx2Header(32, 32, layers: 0, faces: 0, levels: 0),
  _ktx2Header(16, 16, depth: 16, levels: 5),
  _ktx2Header(0, 16),
  _ktx2Header(16, 0),
  _ktx2Header(1, 1, levels: 3),
  _ktx2Header(0xFFFFFFFF, 0xFFFFFFFF, depth: 0xFFFFFFFF, layers: 0xFFFFFFFF),
  _ktx2Header(4096, 4096, levels: 40),
  _ktx2Header(4096, 4096, levels: 1000000),
  _ktx2Header(8, 8, levels: 3, length: 47),
  _ktx2Header(8, 8, levels: 3, length: 48),
  _ktx2Header(8, 8, levels: 3, length: 12),
  Uint8List.fromList([
    0xAB,
    0x4B,
    0x54,
    0x58,
    0x20,
    0x31,
    0x31,
    0xBB,
    0,
    0,
    0,
    0,
  ]),
  Uint8List(0),
  Uint8List(3),
  Uint8List.fromList(utf8.encode('glTF not a picture')),
];

Map<String, Object?> _pictureOut(PictureSize? size) => size == null
    ? const {}
    : {
        'width': big(size.width),
        'height': big(size.height),
        'depth': big(size.depth),
        'layers': big(size.layers),
        'faces': big(size.faces),
        'levels': big(size.levels),
        'pixels': big(size.pixels),
        'text': size.toString(),
      };

Future<List<Map<String, Object?>>> _pictureCases() async => [
  for (final bytes in _pictures)
    await _case('picture', [
      b64(bytes),
    ], () => _pictureOut(pictureSizeOf(bytes))),
  // Every prefix of the header of each kind, which is where a reader that
  // trusts a length goes wrong.
  for (final bytes in [
    _png(300, 200),
    _jpeg(300, 200, before: 1),
    _webpLossy(300, 200),
    _webpLossless(300, 200),
    _webpExtended(300, 200),
    _ktx2Header(300, 200, levels: 4),
  ])
    for (var cut = 0; cut <= min(bytes.length, 60); cut++)
      await _case('picture', [
        b64(bytes.sublist(0, cut)),
      ], () => _pictureOut(pictureSizeOf(bytes.sublist(0, cut)))),
  // One byte changed at a time in the front of each kind.
  for (final bytes in [
    _png(300, 200),
    _jpeg(300, 200, before: 1),
    _webpLossy(300, 200),
    _webpLossless(300, 200),
    _webpExtended(300, 200),
    _ktx2Header(300, 200, levels: 4),
  ])
    for (var at = 0; at < min(bytes.length, 48); at++)
      for (final value in [0x00, 0x01, 0x80, 0xff])
        if (bytes[at] != value)
          await _case('picture', [b64(_with(bytes, at, value))], () {
            return _pictureOut(pictureSizeOf(_with(bytes, at, value)));
          }),
  for (final bytes in _noises)
    await _case('picture', [
      b64(bytes),
    ], () => _pictureOut(pictureSizeOf(bytes))),
];

Uint8List _with(Uint8List bytes, int at, int value) =>
    Uint8List.fromList(bytes)..[at] = value;

// A real signature followed by random bytes, to reach the readers' bounds
// checks with whatever a hostile file puts after the magic.
final _noises = () {
  final random = Random(7);
  final heads = [
    _png(1, 1).sublist(0, 8),
    Uint8List.fromList([0xff, 0xd8]),
    Uint8List.fromList(utf8.encode('RIFF\u0000\u0000\u0000\u0000WEBP')),
    _ktx2Header(1, 1).sublist(0, 12),
  ];
  return [
    for (var i = 0; i < 80; i++)
      Uint8List.fromList([
        ...heads[i % heads.length],
        for (var n = random.nextInt(48); n > 0; n--) random.nextInt(256),
      ]),
  ];
}();

// KTX2 chains.

Uint8List _mipped({
  int width = 256,
  int height = 256,
  int levels = 9,
  int supercompression = 2,
  int describe = 44,
  int keyValues = 20,
  bool metadataLast = false,
}) {
  int sizeOf(int level) {
    final w = (width >> level) < 1 ? 1 : width >> level;
    final h = (height >> level) < 1 ? 1 : height >> level;
    final bytes = w * h ~/ 8;
    return bytes < 8 ? 8 : bytes;
  }

  final indexEnd = 80 + levels * 24;
  var at = indexEnd;
  final dfdAt = metadataLast ? 0 : at;
  if (!metadataLast) at += describe;
  final kvdAt = metadataLast ? 0 : at;
  if (!metadataLast) at += keyValues;

  final offsets = List<int>.filled(levels, 0);
  final lengths = List<int>.filled(levels, 0);
  for (var level = levels - 1; level >= 0; level--) {
    lengths[level] = sizeOf(level);
    offsets[level] = at;
    at += lengths[level];
  }
  final lateDfdAt = metadataLast ? at : 0;
  if (metadataLast) at += describe;

  final bytes = _ktx2Header(width, height, levels: levels, length: at);
  final view = ByteData.sublistView(bytes);
  view.setUint32(36, 1, Endian.little);
  view.setUint32(12, 0, Endian.little);
  view.setUint32(16, 1, Endian.little);
  view.setUint32(44, supercompression, Endian.little);
  view.setUint32(48, metadataLast ? lateDfdAt : dfdAt, Endian.little);
  view.setUint32(52, describe, Endian.little);
  view.setUint32(56, metadataLast ? 0 : kvdAt, Endian.little);
  view.setUint32(60, metadataLast ? 0 : keyValues, Endian.little);
  for (var level = 0; level < levels; level++) {
    final entry = 80 + level * 24;
    view.setUint64(entry, offsets[level], Endian.little);
    view.setUint64(entry + 8, lengths[level], Endian.little);
    view.setUint64(entry + 16, lengths[level] * 4, Endian.little);
    for (var i = 0; i < lengths[level]; i++) {
      bytes[offsets[level] + i] = (level + 1) & 0xFF;
    }
  }
  for (var i = 0; i < describe; i++) {
    bytes[(metadataLast ? lateDfdAt : dfdAt) + i] = 0xD0;
  }
  if (!metadataLast) {
    for (var i = 0; i < keyValues; i++) {
      bytes[kvdAt + i] = 0xCE;
    }
  }
  return bytes;
}

// A chain whose parts are moved by hand, to reach each way the reader can
// refuse a file.
Uint8List _edited(Uint8List bytes, void Function(ByteData view) change) {
  final copy = Uint8List.fromList(bytes);
  change(ByteData.sublistView(copy));
  return copy;
}

Uint8List _withGlobalData() {
  final base = _mipped(width: 64, height: 64, levels: 5);
  final bytes = Uint8List(base.length + 30)..setAll(0, base);
  final view = ByteData.sublistView(bytes);
  view.setUint64(64, base.length, Endian.little);
  view.setUint64(72, 30, Endian.little);
  for (var i = 0; i < 30; i++) {
    bytes[base.length + i] = 0x5A;
  }
  return bytes;
}

final _chains = <(String, Uint8List, int?)>[
  ('default', _mipped(), null),
  ('64 wide, 7 levels', _mipped(width: 64, height: 64, levels: 7), null),
  ('512 square', _mipped(width: 512, height: 512, levels: 10), null),
  ('wide', _mipped(width: 256, height: 32, levels: 9), null),
  ('tall', _mipped(width: 32, height: 256, levels: 9), null),
  ('one row', _mipped(width: 64, height: 0, levels: 7), null),
  ('two levels', _mipped(width: 8, height: 8, levels: 2), null),
  ('one level', _mipped(levels: 1), null),
  ('raw', _mipped(supercompression: 0), null),
  ('metadata last', _mipped(metadataLast: true), null),
  ('no description', _mipped(describe: 0), null),
  ('no key values', _mipped(keyValues: 0), null),
  ('big description', _mipped(describe: 400, keyValues: 300), null),
  ('global data', _withGlobalData(), null),
  (
    'global data before the index end',
    _edited(_withGlobalData(), (v) => v.setUint64(64, 100, Endian.little)),
    null,
  ),
  (
    'a level inside the index',
    _edited(_mipped(), (v) => v.setUint64(80 + 24 * 4, 100, Endian.little)),
    null,
  ),
  (
    'a level of no length',
    _edited(_mipped(), (v) => v.setUint64(80 + 24 * 4 + 8, 0, Endian.little)),
    null,
  ),
  (
    'a level before the metadata end',
    _edited(
      _mipped(),
      (v) => v.setUint64(80 + 24 * 8, 80 + 9 * 24 + 10, Endian.little),
    ),
    null,
  ),
  (
    'an offset that is negative as a signed number',
    _edited(
      _mipped(),
      (v) => v.setUint64(80 + 24 * 2, 0xFFFFFFFFFFFFFFF0, Endian.little),
    ),
    null,
  ),
  (
    'a length that is negative as a signed number',
    _edited(
      _mipped(),
      (v) => v.setUint64(80 + 24 * 2 + 8, 0xFFFFFFFFFFFFFFF0, Endian.little),
    ),
    null,
  ),
  (
    'a description before the index end',
    _edited(_mipped(), (v) => v.setUint32(48, 90, Endian.little)),
    null,
  ),
  (
    'a key-value offset before the index end',
    _edited(_mipped(), (v) => v.setUint32(56, 90, Endian.little)),
    null,
  ),
  (
    'a huge level count',
    _edited(_mipped(), (v) => v.setUint32(40, 0xFFFFFFFF, Endian.little)),
    null,
  ),
  (
    'zero levels',
    _edited(_mipped(), (v) => v.setUint32(40, 0, Endian.little)),
    null,
  ),
  (
    'a depth',
    _edited(_mipped(), (v) => v.setUint32(28, 8, Endian.little)),
    null,
  ),
  (
    'a width of nought',
    _edited(_mipped(), (v) => v.setUint32(20, 0, Endian.little)),
    null,
  ),
  ('default, cut in the middle of the index', _mipped(), 100),
  ('default, cut at the index end', _mipped(), 80 + 9 * 24),
  ('default, cut inside the metadata', _mipped(), 80 + 9 * 24 + 30),
  ('default, cut inside level 8', _mipped(), 400),
  ('default, cut at 79', _mipped(), 79),
  ('default, cut at 80', _mipped(), 80),
  ('default, cut at 0', _mipped(), 0),
  (
    '64, cut one short of level 2',
    _mipped(width: 64, height: 64, levels: 7),
    700,
  ),
];

Map<String, Object?> _chainOut(Uint8List bytes) {
  final chain = Ktx2Chain.read(bytes);
  if (chain == null) return const {};
  return {
    'width': big(chain.width),
    'height': big(chain.height),
    'depth': big(chain.depth),
    'supercompression': big(chain.supercompression),
    'metadataEnd': big(chain.metadataEnd),
    'levels': [
      for (final level in chain.levels)
        [
          big(level.offset),
          big(level.length),
          big(level.uncompressedLength),
          big(level.end),
        ],
    ],
    'levelAtLeast': {
      for (final size in [
        0,
        1,
        2,
        3,
        4,
        8,
        16,
        31,
        32,
        33,
        60,
        64,
        65,
        100,
        128,
        255,
        256,
        257,
        512,
        4096,
      ])
        '$size': chain.levelAtLeast(size),
    },
    'bytesFor': [
      for (var level = -1; level <= chain.levels.length + 1; level++)
        if (level >= 0 && level <= chain.levels.length)
          big(chain.bytesFor(level)),
    ],
    'prefixes': [
      for (var level = -1; level <= chain.levels.length + 1; level++)
        () {
          final out = chain.prefix(bytes, level);
          return out == null
              ? null
              : [big(out.length), ContentHash.of(out).hex];
        }(),
    ],
  };
}

Future<List<Map<String, Object?>>> _chainCases() async => [
  for (final (name, bytes, cut) in _chains)
    await _case('ktx2', [
      name,
      b64(cut == null ? bytes : bytes.sublist(0, cut)),
    ], () => _chainOut(cut == null ? bytes : bytes.sublist(0, cut))),
];

// The range queue. A script is the wants made in one go, the fetch that
// answers them, and what the batching is. Nothing is made after the first
// request goes out, so the requests are the same every run.

Uint8List _fileBytes(int at, int bytes) => Uint8List.fromList([
  for (var i = 0; i < bytes; i++) ((at + i) * 7 + 3) & 0xFF,
]);

typedef _Batching = ({int gap, int batch, int parallel, int delay});

Future<Object?> _queueRun(
  _Batching batching,
  List<List<Object?>> wants,
  List<int> failing,
) async {
  final calls = <List<int>>[];
  final queue = RangeQueue(
    fetch: (at, bytes) async {
      calls.add([at, bytes]);
      if (failing.contains(calls.length - 1)) throw StateError('no');
      return _fileBytes(at, bytes);
    },
    batching: RangeBatching(
      mergeGap: batching.gap,
      maxBatch: batching.batch,
      parallel: batching.parallel,
      delay: Duration(milliseconds: batching.delay),
    ),
  );
  final answers = <Future<Object?>>[];
  for (final want in wants) {
    final at = want[0]! as int;
    final bytes = want[1]! as int;
    final priority = want[2] == 'later'
        ? RangePriority.later
        : RangePriority.now;
    answers.add(
      queue
          .want(at, bytes, priority: priority)
          .then<Object?>((got) => [got.length, ContentHash.of(got).hex])
          .catchError((Object _) => 'failed'),
    );
  }
  final got = await Future.wait(answers);
  return {
    'calls': calls,
    'answers': got,
    'matches': [
      for (var i = 0; i < wants.length; i++)
        got[i] is List &&
            (got[i]! as List)[1] ==
                ContentHash.of(
                  _fileBytes(wants[i][0]! as int, wants[i][1]! as int),
                ).hex,
    ],
  };
}

const _normal = (gap: 16384, batch: 1048576, parallel: 4, delay: 2);

final _queueScripts = <(_Batching, List<List<Object?>>, List<int>)>[
  (
    _normal,
    [
      [0, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [10, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [100, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [16394, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [16393, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [100, 10, 'now'],
      [0, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 100, 'now'],
      [10, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 100, 'now'],
      [10, 200, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [0, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'later'],
      [0, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [0, 10, 'later'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [0, 11, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'later'],
      [10, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'later'],
      [10, 10, 'later'],
    ],
    [],
  ),
  (
    _normal,
    [
      [500, 10, 'later'],
      [0, 10, 'now'],
      [10, 10, 'later'],
    ],
    [],
  ),
  (
    (gap: 0, batch: 1048576, parallel: 4, delay: 2),
    [
      [0, 10, 'now'],
      [10, 10, 'now'],
      [21, 10, 'now'],
    ],
    [],
  ),
  (
    (gap: 5, batch: 1048576, parallel: 4, delay: 2),
    [
      [0, 10, 'now'],
      [15, 10, 'now'],
      [31, 10, 'now'],
    ],
    [],
  ),
  (
    (gap: 16384, batch: 25, parallel: 4, delay: 2),
    [
      [0, 10, 'now'],
      [10, 10, 'now'],
      [20, 10, 'now'],
      [30, 10, 'now'],
    ],
    [],
  ),
  (
    (gap: 16384, batch: 30, parallel: 4, delay: 2),
    [
      [0, 10, 'now'],
      [10, 10, 'now'],
      [20, 10, 'now'],
      [30, 10, 'now'],
    ],
    [],
  ),
  (
    (gap: 16384, batch: 5, parallel: 4, delay: 2),
    [
      [0, 10, 'now'],
      [10, 10, 'now'],
    ],
    [],
  ),
  (
    (gap: 0, batch: 1, parallel: 1, delay: 2),
    [
      for (var i = 0; i < 8; i++) [i * 100, 10, 'now'],
    ],
    [],
  ),
  (
    (gap: 0, batch: 1, parallel: 2, delay: 2),
    [
      for (var i = 0; i < 8; i++) [i * 100, 10, i.isEven ? 'now' : 'later'],
    ],
    [],
  ),
  (
    (gap: 0, batch: 1048576, parallel: 3, delay: 0),
    [
      for (var i = 9; i >= 0; i--) [i * 100, 10, 'now'],
    ],
    [],
  ),
  (
    (gap: 0, batch: 1048576, parallel: 4, delay: 2),
    [
      for (var i = 0; i < 12; i++) [i * 100, 10, i % 3 == 0 ? 'later' : 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [10, 10, 'now'],
      [100, 10, 'now'],
    ],
    [0],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [100, 10, 'now'],
      [200000, 10, 'now'],
    ],
    [1],
  ),
  (
    _normal,
    [
      [0, 10, 'now'],
      [100, 10, 'now'],
    ],
    [0, 1],
  ),
  (
    _normal,
    [
      [-1, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 0, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, -5, 'now'],
      [0, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [1 << 40, 10, 'now'],
      [(1 << 40) + 10, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 2000000, 'now'],
      [2000000, 10, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 1048576, 'now'],
      [1048576, 1, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 1048575, 'now'],
      [1048575, 1, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      [0, 1048575, 'now'],
      [1048575, 2, 'now'],
    ],
    [],
  ),
  (
    _normal,
    [
      for (var i = 0; i < 30; i++)
        [(i * 7919) % 100000, 1 + (i * 31) % 50, i.isEven ? 'now' : 'later'],
    ],
    [],
  ),
  (
    _normal,
    [
      for (var i = 0; i < 30; i++)
        [
          (i * 104729) % 1000000,
          1 + (i * 131) % 5000,
          i % 5 == 0 ? 'later' : 'now',
        ],
    ],
    [],
  ),
];

final _badBatching = <_Batching>[
  (gap: -1, batch: 1, parallel: 1, delay: 0),
  (gap: 0, batch: 0, parallel: 1, delay: 0),
  (gap: 0, batch: 1, parallel: 0, delay: 0),
  (gap: 0, batch: 1, parallel: 1, delay: -1),
  (gap: 0, batch: 1, parallel: 1, delay: 0),
];

Future<List<Map<String, Object?>>> _queueCases() async => [
  for (final (batching, wants, failing) in _queueScripts)
    await _case('queue', [
      {
        'gap': batching.gap,
        'batch': batching.batch,
        'parallel': batching.parallel,
        'delay': batching.delay,
      },
      wants,
      failing,
    ], () => _queueRun(batching, wants, failing)),
  for (final batching in _badBatching)
    await _case(
      'queue.new',
      [
        {
          'gap': batching.gap,
          'batch': batching.batch,
          'parallel': batching.parallel,
          'delay': batching.delay,
        },
      ],
      () {
        RangeQueue(
          fetch: (at, bytes) async => Uint8List(bytes),
          batching: RangeBatching(
            mergeGap: batching.gap,
            maxBatch: batching.batch,
            parallel: batching.parallel,
            delay: Duration(milliseconds: batching.delay),
          ),
        );
        return true;
      },
    ),
];

// The map transport. Each script is a list of requests sent in order to a
// page, and what came back, with the body read to its end.

Future<Map<String, Object?>> _bodyOf(FetchReply reply) async {
  final chunks = <int>[];
  final all = BytesBuilder();
  Object? failure;
  try {
    await for (final chunk in reply.body) {
      chunks.add(chunk.length);
      all.add(chunk);
    }
  } on Object catch (error) {
    failure = error is SocketFailure ? error.reason : '$error';
  }
  final bytes = all.toBytes();
  return {
    'status': reply.status,
    'etag': reply.etag,
    'length': reply.length,
    'from': reply.from,
    'notModified': reply.isNotModified,
    'mayWorkLater': reply.mayWorkLater,
    'chunks': chunks,
    'hash': ContentHash.of(bytes).hex,
    'size': bytes.length,
    'failure': failure,
  };
}

final _page = Uint8List.fromList([for (var i = 0; i < 100; i++) i]);

Future<Object?> _transportRun(
  Map<String, Object?> pageSpec,
  List<List<Object?>> requests,
) async {
  final url = Uri.parse('https://e.example.com/a.bin');
  final transport = MapTransport({
    url: TransportPage(
      _page,
      etag: pageSpec['etag'] as String?,
      cut: (pageSpec['cut'] as int?) ?? 0,
      stopAfter: pageSpec['stopAfter'] as int?,
      faults: [
        for (final fault in (pageSpec['faults'] as List?) ?? const [])
          fault is int ? Fault.status(fault) : const Fault.thrown(),
      ],
    ),
  });
  final out = <Object?>[];
  for (final request in requests) {
    final target = request[0] == 'missing'
        ? Uri.parse('https://e.example.com/missing.bin')
        : url;
    try {
      final reply = await transport.send(
        FetchRequest(
          target,
          ifNoneMatch: request[1] as String?,
          from: request[2] as int?,
          through: request[3] as int?,
        ),
      );
      out.add(await _bodyOf(reply));
    } on SocketFailure catch (error) {
      out.add({'threw': error.reason});
    }
  }
  return {
    'replies': out,
    'sent': [
      for (final sent in transport.sent)
        [sent.url.toString(), sent.ifNoneMatch, sent.from, sent.through],
    ],
  };
}

final _transportScripts = <(Map<String, Object?>, List<List<Object?>>)>[
  (
    {},
    [
      ['a', null, null, null],
    ],
  ),
  (
    {},
    [
      ['missing', null, null, null],
    ],
  ),
  (
    {'etag': '"v1"'},
    [
      ['a', '"v1"', null, null],
      ['a', '"v2"', null, null],
      ['a', null, null, null],
    ],
  ),
  (
    {},
    [
      ['a', '"v1"', null, null],
    ],
  ),
  (
    {'cut': 30},
    [
      ['a', null, null, null],
    ],
  ),
  (
    {'cut': 100},
    [
      ['a', null, null, null],
    ],
  ),
  (
    {'cut': 1},
    [
      ['a', null, 95, null],
    ],
  ),
  (
    {},
    [
      ['a', null, 10, null],
    ],
  ),
  (
    {},
    [
      ['a', null, 10, 19],
    ],
  ),
  (
    {},
    [
      ['a', null, 0, 9],
    ],
  ),
  (
    {},
    [
      ['a', null, 0, 0],
    ],
  ),
  (
    {},
    [
      ['a', null, 90, 500],
    ],
  ),
  (
    {},
    [
      ['a', null, 100, null],
    ],
  ),
  (
    {},
    [
      ['a', null, 101, null],
    ],
  ),
  (
    {},
    [
      ['a', null, 100, 120],
    ],
  ),
  (
    {'cut': 7},
    [
      ['a', null, 20, 60],
    ],
  ),
  (
    {
      'faults': [503],
    },
    [
      ['a', null, null, null],
      ['a', null, null, null],
    ],
  ),
  (
    {
      'faults': [404, 429, 408, 425, 500, 599, 403, 400],
    },
    [
      for (var i = 0; i < 9; i++) ['a', null, null, null],
    ],
  ),
  (
    {
      'faults': ['x'],
    },
    [
      ['a', null, null, null],
      ['a', null, null, null],
    ],
  ),
  (
    {
      'faults': [503, 'x', 200],
    },
    [
      for (var i = 0; i < 4; i++) ['a', null, null, null],
    ],
  ),
  (
    {'stopAfter': 40},
    [
      ['a', null, null, null],
      ['a', null, 40, null],
    ],
  ),
  (
    {'stopAfter': 40, 'cut': 15},
    [
      ['a', null, null, null],
      ['a', null, null, null],
    ],
  ),
  (
    {'stopAfter': 500},
    [
      ['a', null, null, null],
    ],
  ),
  (
    {'stopAfter': 0},
    [
      ['a', null, 10, 50],
      ['a', null, 10, 50],
    ],
  ),
  (
    {'stopAfter': -3},
    [
      ['a', null, null, null],
    ],
  ),
];

Future<List<Map<String, Object?>>> _transportCases() async => [
  for (final (spec, requests) in _transportScripts)
    await _case('transport', [
      spec,
      requests,
    ], () => _transportRun(spec, requests)),
];

Future<void> main(List<String> args) async {
  final cases = <Map<String, Object?>>[
    ...await _urlCases(),
    ...await _originCases(),
    ...await _recordCases(),
    ...await _pictureCases(),
    ...await _chainCases(),
    ...await _queueCases(),
    ...await _transportCases(),
  ];
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'net',
    cases: cases,
  );
}
