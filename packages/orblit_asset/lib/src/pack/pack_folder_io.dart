// The half of the conditional export in pack_folder.dart for platforms with a
// file system.
import 'dart:async';
import 'dart:io';
import 'dart:math';

import '../content_hash.dart';
import '../net/fetcher.dart' show FetchProgress;
import '../net/policy.dart';
import '../net/transport.dart';
import 'asset_pack.dart';
import 'pack_download_io.dart';
import 'pack_file_io.dart';
import 'pack_listing.dart';
import 'pack_offer.dart';

/// A folder of packs downloaded after install.
///
/// Every pack in it is named by its hash, `<hash>.opack`, and a download in
/// progress is `<hash>.part`. A file only gets a pack's name once it has been
/// checked, so a crash or a power cut part way through leaves a file nothing
/// will mount, and the next [download] of the same offer carries on from it.
///
/// The folder holds packs the game fetched. A mods folder a player drops files
/// into is a different thing: open those with `FilePackBytes` and
/// `AssetPack.open`, whatever they are called.
final class PackFolder {
  PackFolder(this.path);

  /// The folder. It is made when the first pack arrives.
  final String path;

  final Map<ContentHash, Future<AssetPack>> _running = {};

  /// Every pack here, and everything here that is not one.
  ///
  /// A file whose contents do not hash to its name is reported and not
  /// opened, since the name is what makes the folder safe to trust.
  Future<PackListing> list() async {
    final files = await _files();
    final packs = <AssetPack>[];
    final unreadable = <UnreadablePack>[];
    for (final file in files) {
      if (!_nameOf(file).endsWith(_packSuffix)) continue;
      try {
        packs.add(await _openNamed(file));
      } on FormatException catch (error) {
        unreadable.add(UnreadablePack(file.path, error.message));
      } on FileSystemException catch (error) {
        unreadable.add(UnreadablePack(file.path, error.message));
      }
    }
    return PackListing(
      packs: packs,
      unreadable: unreadable,
      unfinished: [for (final file in files) ?_hashOf(file, _partSuffix)],
    );
  }

  /// Deletes the pack with [hash] and any half-finished download of it, and
  /// says whether either was there.
  ///
  /// Call it before the pack is mounted. A mounted pack holds its file open.
  Future<bool> remove(ContentHash hash) async {
    final packGone = await _delete(_packFile(hash));
    final partGone = await _delete(_partFile(hash));
    return packGone || partGone;
  }

  /// Fetches the pack [offer] names and answers it opened, with every blob
  /// checked.
  ///
  /// Returns at once when the pack is already here. A download that was cut
  /// off, in this run or an earlier one, resumes from the bytes held. The
  /// pack has to hash to `offer.hash` and every blob in it has to match the
  /// directory. A failure of either throws and leaves no file with a pack's
  /// name.
  ///
  /// A connection that failed, or a server that answered 5xx, 408, 425 or
  /// 429, is tried again up to [attempts] times in all, waiting [backoff]
  /// and then twice as long each time, give or take a fifth. A resumed
  /// attempt costs only the bytes still missing, so more attempts are cheap.
  /// A 404, a host [origin] does not allow, a pack over the policy's
  /// `maxBytes` and a pack that is not [offer]'s throw at once.
  ///
  /// A second call for the same hash while one is running shares it, and its
  /// [onProgress] is not called.
  Future<AssetPack> download(
    PackOffer offer, {
    required AssetTransport transport,
    required AssetOrigin origin,
    int attempts = 5,
    Duration backoff = const Duration(seconds: 1),
    Random? jitter,
    void Function(FetchProgress progress)? onProgress,
  }) => _running[offer.hash] ??=
      _fetch(
        offer,
        PackDownload(
          offer: offer,
          transport: transport,
          origin: origin,
          part: _partFile(offer.hash),
          attempts: attempts,
          backoff: backoff,
          jitter: jitter,
          onProgress: onProgress,
        ),
      ).whenComplete(() {
        // A block, not an arrow. `remove` answers the future being
        // completed, and a callback that returns a future is waited on.
        _running.remove(offer.hash);
      });

  Future<AssetPack> _fetch(PackOffer offer, PackDownload download) async {
    await Directory(path).create(recursive: true);
    final held = await _heldCopy(offer.hash);
    if (held != null) return held;
    // A file with the pack's name and other contents is in the way of the
    // rename that finishes a download.
    await _delete(_packFile(offer.hash));
    return download.run();
  }

  /// The pack already filed under [hash], or null when there is none or what
  /// has its name is not that pack.
  Future<AssetPack?> _heldCopy(ContentHash hash) async {
    final file = _packFile(hash);
    if (!await file.exists()) return null;
    try {
      final pack = await _openFile(file);
      return pack.hash == hash ? pack : null;
    } on FormatException {
      return null;
    }
  }

  static Future<bool> _delete(File file) async {
    if (!await file.exists()) return false;
    await file.delete();
    return true;
  }

  /// Opens [file], which has to be named for the hash of what is in it.
  Future<AssetPack> _openNamed(File file) async {
    final pack = await _openFile(file);
    if (_nameOf(file) != '${pack.hash.hex}$_packSuffix') {
      throw FormatException(
        'its name is not the hash of what is in it, which is '
        '${pack.hash.hex}',
      );
    }
    return pack;
  }

  /// The files in the folder, by name, or none when it is not there yet.
  Future<List<File>> _files() async {
    final directory = Directory(path);
    if (!await directory.exists()) return const [];
    final files = [
      await for (final entry in directory.list())
        if (entry is File) entry,
    ];
    return files..sort((a, b) => a.path.compareTo(b.path));
  }

  File _packFile(ContentHash hash) =>
      File('$path${Platform.pathSeparator}${hash.hex}$_packSuffix');

  File _partFile(ContentHash hash) =>
      File('$path${Platform.pathSeparator}${hash.hex}$_partSuffix');

  static ContentHash? _hashOf(File file, String suffix) {
    final name = _nameOf(file);
    if (!name.endsWith(suffix)) return null;
    try {
      return ContentHash.parse(name.substring(0, name.length - suffix.length));
    } on FormatException {
      return null;
    }
  }

  static String _nameOf(File file) => file.uri.pathSegments.last;
}

const _packSuffix = '.opack';
const _partSuffix = '.part';

/// Opens the pack in [file] and lets go of the file, which a read opens
/// again. A folder with a dozen packs in it does not hold a dozen handles
/// for the ones that are never read.
Future<AssetPack> _openFile(File file) async {
  final bytes = FilePackBytes(file.path);
  try {
    return await AssetPack.open(bytes);
  } finally {
    await bytes.close();
  }
}
