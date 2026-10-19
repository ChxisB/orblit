/// Square cells on the ground plane.
///
/// A world made of cells has to answer the same two questions in several
/// places: which cell is this point in, and which cells are near it. The
/// network's interest rule, the regions a host simulates and the scene cells a
/// game streams in all ask them, so the answer is kept here, with nothing
/// else, and each of them takes it from one place.
library;

export 'src/grid.dart' show Cell, Grid;
export 'src/window.dart' show CellPlan, CellWindow;
