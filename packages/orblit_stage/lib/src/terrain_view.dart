import 'dart:typed_data';

import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_terrain/orblit_terrain.dart';

/// Turns ground kept as data into the terrain the renderer draws.
///
/// The regions' maps are handed over as they are, not copied: a region the
/// renderer already holds costs nothing to pass again, and one that was
/// edited is sent because its revision moved, which every edit to a
/// [TerrainRegion] does.
///
/// A set names its images by path, and [pixels] turns a path into the image
/// itself, decoded: RGBA bytes a row at a time, square, and every image of
/// every set the same size. An image it cannot supply draws as the plain one.
/// [picturesRevision] must move whenever an image's pixels change and the
/// number of sets and their size do not, since nothing else says they did.
///
/// A terrain the renderer cannot draw — regions smaller than it takes, too
/// many of them, images of different sizes — throws [ArgumentError] saying
/// why, rather than drawing nothing.
OrblitTerrain terrainFrom(
  Terrain terrain, {
  required int key,
  Uint8List? Function(String path)? pixels,
  int picturesRevision = 0,
  int meshSize = 64,
  int levels = 6,
  bool castShadows = true,
  bool receiveShadows = true,
}) {
  Uint8List? image(String? path) =>
      path == null || pixels == null ? null : pixels(path);

  return OrblitTerrain(
    key: key,
    regions: [
      for (final region in terrain.regions)
        OrblitTerrainRegion(
          x: region.key.x,
          z: region.key.z,
          heights: region.heights,
          cover: region.cover,
          colour: region.colour,
          revision: region.revision,
        ),
    ],
    regionSize: terrain.regionSize,
    spacing: terrain.spacing,
    sets: [
      for (final set in terrain.sets)
        OrblitTerrainSet(
          albedo: image(set.albedo),
          normal: image(set.normal),
          tileSize: set.tileSize,
          triplanar: set.triplanar,
        ),
    ],
    picturesRevision: picturesRevision,
    blendSharpness: terrain.blendSharpness,
    autoSteep: terrain.autoCover.steep,
    autoFlat: terrain.autoCover.flat,
    autoSlope: terrain.autoCover.slope,
    autoHeightFalloff: terrain.autoCover.heightFalloff,
    meshSize: meshSize,
    levels: levels,
    castShadows: castShadows,
    receiveShadows: receiveShadows,
  );
}
