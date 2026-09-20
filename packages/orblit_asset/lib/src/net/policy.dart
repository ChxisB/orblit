import '../asset_id.dart';

/// A fetch that was not allowed to happen, and why.
///
/// Thrown before anything is sent rather than returned as a failed reply,
/// because a refusal here is never about the network. It means the program
/// asked for something it is not configured to ask for — a host nobody listed,
/// a plain-text URL, a path climbing out of its own directory — and that is a
/// mistake in the program or an attempt on it, not a connection to retry.
class FetchRefused implements Exception {
  const FetchRefused(this.url, this.reason);

  /// What was asked for. A string rather than a [Uri] because some of what is
  /// refused cannot be parsed as one.
  final String url;

  /// One sentence, written to be read by whoever has to fix it.
  final String reason;

  @override
  String toString() => 'Refused to fetch $url: $reason.';
}

/// What an app will fetch, and how much of it.
///
/// Every field is a limit rather than a permission, and the defaults are the
/// cautious ones. A game that loads assets from a server is downloading
/// somebody else's bytes and handing them to a decoder — a decoder written in
/// C, for several of the formats. The cheapest defence is to not fetch the
/// thing at all, and the second cheapest is to stop reading it before it has
/// been given to anything that parses.
class FetchPolicy {
  const FetchPolicy({
    this.hosts = const {},
    this.allowInsecure = false,
    this.maxBytes = 256 << 20,
    this.maxPixels = 64 << 20,
  }) : assert(maxBytes > 0),
       assert(maxPixels > 0);

  /// Hosts that may be fetched from besides the origin's own.
  ///
  /// Usually empty. An origin already allows the host its base URL names, so
  /// this is for the second place a project keeps things — textures on a CDN
  /// beside a manifest served from the app's own domain. Matched exactly and
  /// case-insensitively: `cdn.example.com` does not allow
  /// `evil.cdn.example.com`, because a suffix match is how an allow-list
  /// becomes an allow-anything.
  final Set<String> hosts;

  /// Whether `http:` is allowed as well as `https:`.
  ///
  /// Off. A texture arriving over plain HTTP can be replaced in flight by
  /// anyone between here and the server, and a texture is bytes fed to a
  /// decoder. Worth turning on for a local test server and nowhere else.
  final bool allowInsecure;

  /// The most bytes one asset may be.
  ///
  /// Checked against the length the server declares before the body is read,
  /// and again as the body arrives — a server that says one thing and sends
  /// another is exactly the case worth catching, and a server that declares no
  /// length at all is the case where only the second check exists.
  final int maxBytes;

  /// The most pixels a picture may have, across every mip level.
  ///
  /// Separate from [maxBytes] because the two are not related: a 64,000 by
  /// 64,000 PNG of flat colour compresses to a few hundred kilobytes and
  /// decodes to sixteen gigabytes. A size limit stops a slow download; only a
  /// dimension limit stops that.
  final int maxPixels;

  /// This policy with [hosts] added to the ones already allowed.
  FetchPolicy allowing(Iterable<String> more) => FetchPolicy(
    hosts: {...hosts, ...more},
    allowInsecure: allowInsecure,
    maxBytes: maxBytes,
    maxPixels: maxPixels,
  );
}

/// Where a project's assets are served from.
///
/// Holds the base URL and the [policy] that applies to anything reached from
/// it, and is the only thing in this package that turns an [AssetId] into a
/// URL. Keeping that in one place is what makes the rules checkable: every
/// fetch goes through [urlOf] or [beside], and neither can produce a URL the
/// policy would not allow.
class AssetOrigin {
  /// An origin serving assets under [base].
  ///
  /// [base] is treated as a directory whether or not it ends in a slash, since
  /// `https://example.com/assets` and `https://example.com/assets/` are the
  /// same intention and only one of them resolves the way anybody expects.
  AssetOrigin(Uri base, {this.policy = const FetchPolicy()})
    : base = base.path.endsWith('/')
          ? base
          : base.replace(path: '${base.path}/') {
    _check(this.base, this.base.toString());
  }

  /// An origin from a URL written as text, throwing [FetchRefused] when that
  /// is not a URL this would ever fetch from.
  factory AssetOrigin.parse(String base, {FetchPolicy? policy}) {
    final url = Uri.tryParse(base);
    if (url == null) {
      throw FetchRefused(base, 'it is not a URL');
    }
    return AssetOrigin(url, policy: policy ?? const FetchPolicy());
  }

  /// The base, always ending in a slash.
  final Uri base;

  final FetchPolicy policy;

  /// Where [id] is served from.
  ///
  /// The id's own path, escaped, under [base]. An id cannot contain `..` or
  /// start with a slash — [AssetId] refuses both — so this cannot leave the
  /// base by construction, and the check below is there for the day that
  /// stops being true rather than because it can fire today.
  Uri urlOf(AssetId id) => _under(Uri.parse(_escape(id.toString())), '$id');

  /// What a file served from [from] means by the relative reference [uri].
  ///
  /// This is the glTF case: a model at `https://cdn.example/models/robot.gltf`
  /// naming `../textures/metal.png` in a buffer or image. The reference is
  /// resolved the way a browser would, and then held to the same rules as
  /// anything else — which is the point, because the reference comes out of a
  /// file somebody else wrote.
  ///
  /// Refuses anything that leaves [base], names another scheme, or reaches the
  /// local machine. `file:///etc/passwd` and `../../../../etc/passwd` are the
  /// same attempt written two ways, and a model is an untrusted document: it
  /// arrived over the network and it is asking to open something.
  Uri beside(Uri from, String uri) {
    final reference = Uri.tryParse(uri);
    if (reference == null) {
      throw FetchRefused(uri, 'it is not a URL');
    }
    return _under(reference, uri, from: from);
  }

  /// Whether [url] is one this origin would fetch, without throwing.
  ///
  /// For deciding between two sources rather than for guarding a fetch: the
  /// fetch itself is guarded by [urlOf] and [beside], which say why.
  bool allows(Uri url) {
    try {
      _check(url, url.toString());
      return true;
    } on FetchRefused {
      return false;
    }
  }

  Uri _under(Uri reference, String wrote, {Uri? from}) {
    final url = (from ?? base).resolveUri(reference);
    _check(url, wrote);

    // Inside the base, not merely on the same host. A CDN serves more than one
    // project, and a reference that climbs out of this project's folder into
    // another one resolves perfectly and is still wrong.
    if (url.host.toLowerCase() == base.host.toLowerCase() &&
        !url.path.startsWith(base.path)) {
      throw FetchRefused(
        wrote,
        'it resolves to ${url.path}, which is outside ${base.path}',
      );
    }
    return url;
  }

  void _check(Uri url, String wrote) {
    final scheme = url.scheme.toLowerCase();
    if (scheme != 'https' && !(scheme == 'http' && policy.allowInsecure)) {
      throw FetchRefused(
        wrote,
        scheme == 'http'
            ? 'it is plain HTTP, and this policy does not allow that. Set '
                  'allowInsecure only for a server on this machine'
            : scheme.isEmpty
            ? 'it names no scheme, so it is not a URL an asset can be fetched '
                  'from'
            : 'assets are fetched over HTTPS, and this is "$scheme:"',
      );
    }

    final host = url.host.toLowerCase();
    if (host.isEmpty) {
      throw FetchRefused(wrote, 'it names no host');
    }
    // An origin always allows its own host; the policy's list is the extras.
    // Written this way round so the ordinary case needs no list at all, and a
    // list that is empty means "only where my assets are" rather than
    // "anywhere".
    if (host != base.host.toLowerCase() &&
        !policy.hosts.any((one) => one.toLowerCase() == host)) {
      throw FetchRefused(
        wrote,
        'nothing allows the host "$host". Add it to FetchPolicy.hosts if it '
        'is meant to serve this project',
      );
    }
  }

  /// Percent-escapes an id's path without escaping its slashes.
  ///
  /// [Uri.encodeFull] would leave a `?` or `#` alone, and an asset called
  /// `notes#2.png` would then be fetched as `notes` with a fragment. Escaping
  /// each segment on its own is the only way to keep the separators separating
  /// and everything else literal.
  static String _escape(String path) =>
      path.split('/').map(Uri.encodeComponent).join('/');

  @override
  String toString() => 'AssetOrigin($base)';
}
