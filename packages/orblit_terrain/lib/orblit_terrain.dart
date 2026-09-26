/// Ground as data.
///
/// A [Terrain] is a grid of heights kept in square [TerrainRegion]s, made
/// only where there is ground, so a coastline costs the sea nothing. Each
/// texel has a height, a [Cover] saying which texture sets lie on it and
/// how, and a [GroundColour] tinting it. The settings are one `.oterrain`
/// file and each region an `.oregion` file beside it.
///
/// [Terrain.heightAt] and [Terrain.normalAt] read the same heights the
/// renderer draws, the same way, so what stands on the ground stands on
/// what is seen. They need no physics.
///
/// A [ScatterLayer] is a rule for grass, stones or trees over the ground,
/// kept in [Terrain.scatter]; a [ScatterPlacer] works out where each one
/// stands and places a region again when its ground changes.
library;

export 'src/brush.dart' show Brush, BrushTool;
export 'src/cover.dart' show Cover;
export 'src/ground_colour.dart' show GroundColour;
export 'src/patch.dart' show TerrainLayer, TerrainPatch, TerrainRecorder;
export 'src/region.dart'
    show
        RegionFormatException,
        RegionKey,
        RegionLoad,
        RegionMigration,
        RegionParts,
        TerrainRegion,
        regionExtension;
export 'src/scatter.dart' show ScatterGroup, ScatterPlacer;
export 'src/scatter_layer.dart' show ScatterLayer;
export 'src/stroke.dart' show TerrainStroke;
export 'src/terrain.dart'
    show
        AutoCover,
        Terrain,
        TerrainFormatException,
        TerrainLoad,
        TerrainMigration,
        TerrainSet,
        terrainExtension;
