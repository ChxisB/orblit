import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_manifest.dart';
import '../asset_source.dart';
import '../content_hash.dart';
import '../importer.dart';
import 'asset_pack.dart';

/// Thrown when a pack is offered to a [MountedPacks] it does not belong in.
///
/// The pack is fine. It is the wrong pack for this game, this target or this
/// place in the chain, and mounting it anyway would read the wrong bytes.
final class PackMountRefused implements Exception {
  const PackMountRefused(this.message);

  final String message;

  @override
  String toString() => message;
}

/// The packs of one game, searched as one [AssetSource].
///
/// A chain is a pack with no base, then patches, each built against the one
/// before it. Mods sit above the whole chain, later mods above earlier ones.
/// Which pack answers for an id is worked out when a pack is mounted, so a
/// read is one map lookup however many packs there are.
///
/// Order of mounting does not change the answer. A patch mounted after a mod
/// still lands in the chain beneath it, so an app that downloads a patch late
/// cannot undo a mod by doing so.
///
/// Nothing is unmounted. A mod switched off takes a restart.
final class MountedPacks implements AssetSource {
  MountedPacks({required CookTarget target}) : _target = target.name;

  final String _target;
  final List<AssetPack> _chain = [];
  final List<AssetPack> _mods = [];
  Map<AssetId, AssetPack> _owner = const {};
  AssetManifest _manifest = AssetManifest();

  /// The hash a patch must name as its base: the top of the chain, or null
  /// before [start].
  ContentHash? get chainHash => _chain.isEmpty ? null : _chain.last.hash;

  /// What every mounted asset is, with removals and mods spent.
  AssetManifest get manifest => _manifest;

  /// The mounted pack that answers for [id], or null when none does.
  ///
  /// A mod above the chain is the owner of an id it covers, and a patch is
  /// the owner of one it replaced.
  AssetPack? ownerOf(AssetId id) => _owner[id];

  /// Mounts the pack the chain stands on. It has no base.
  void start(AssetPack pack) {
    _checkTarget(pack);
    if (_chain.isNotEmpty) {
      throw const PackMountRefused(
        'The chain has already started. Use patch for a pack built against '
        'the one on top.',
      );
    }
    if (pack.base != null) {
      throw const PackMountRefused(
        'This pack is a patch and the chain has nothing under it yet. Start '
        'with the pack it was built against.',
      );
    }
    _chain.add(pack);
    _resolve();
  }

  /// Mounts a pack built against the top of the chain.
  void patch(AssetPack pack) {
    _checkTarget(pack);
    final top = chainHash;
    if (top == null) {
      throw const PackMountRefused(
        'There is nothing for a patch to apply to. Start the chain first.',
      );
    }
    if (pack.base == null) {
      throw const PackMountRefused(
        'This pack has no base, so it is a whole pack and not a patch. Use '
        'addMod to put it above the chain.',
      );
    }
    if (pack.base != top) {
      throw PackMountRefused(
        'This patch was built against ${_short(pack.base!)} and the top of '
        'the chain is ${_short(top)}.',
      );
    }
    _chain.add(pack);
    _resolve();
  }

  /// Mounts a pack above the chain. It has no base.
  void addMod(AssetPack pack) {
    _checkTarget(pack);
    if (pack.base != null) {
      throw const PackMountRefused(
        'This pack is a patch, built against another pack. A mod stands on '
        'its own.',
      );
    }
    if (_mods.any((mod) => mod.hash == pack.hash) ||
        _chain.any((link) => link.hash == pack.hash)) {
      throw PackMountRefused(
        'The pack ${_short(pack.hash)} is already mounted.',
      );
    }
    _mods.add(pack);
    _resolve();
  }

  @override
  Future<Uint8List> read(AssetId id) async {
    final owner = _owner[id];
    if (owner == null) throw AssetNotFound(id, reason: _whyNot());
    return owner.read(id);
  }

  @override
  Future<bool> exists(AssetId id) async => _owner.containsKey(id);

  void _checkTarget(AssetPack pack) {
    if (pack.target != _target) {
      throw PackMountRefused(
        'A pack for ${pack.target} cannot join packs for $_target.',
      );
    }
  }

  void _resolve() {
    final owner = <AssetId, AssetPack>{};
    final entries = <AssetId, AssetEntry>{};
    for (final pack in [..._chain, ..._mods]) {
      for (final id in pack.removed) {
        owner.remove(id);
        entries.remove(id);
      }
      for (final MapEntry(:key, :value) in pack.manifest.entries.entries) {
        owner[key] = pack;
        entries[key] = value;
      }
    }
    _owner = owner;
    _manifest = AssetManifest(entries: entries);
  }

  String _whyNot() => _chain.isEmpty && _mods.isEmpty
      ? 'no pack is mounted'
      : 'none of the ${_chain.length + _mods.length} mounted packs has it';
}

String _short(ContentHash hash) => hash.hex.substring(0, 12);
