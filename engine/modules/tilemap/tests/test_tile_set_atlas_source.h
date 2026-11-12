/**************************************************************************/
/*  test_tile_set_atlas_source.h                                          */

#pragma once

#include "../tile_set.h"

#include "tests/test_macros.h"
#include "tests/test_tools.h"

namespace TestTileSetAtlasSource {

TEST_CASE("[TileSetAtlasSource] A stored tile outside the texture is not half created on load") {
	Ref<TileSetAtlasSource> source;
	source.instantiate();

	ErrorDetector errors;
	ERR_PRINT_OFF;
	source->set("21:21/next_alternative_id", 1);
	source->set("21:21/0/probability", 0.5);
	source->set("21:21/1/probability", 0.5);
	ERR_PRINT_ON;

	CHECK(errors.has_error);
	CHECK_FALSE(source->has_tile(Vector2i(21, 21)));
	CHECK(source->get_tiles_count() == 0);
}

} // namespace TestTileSetAtlasSource
