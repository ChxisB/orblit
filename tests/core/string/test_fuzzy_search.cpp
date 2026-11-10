/**************************************************************************/
/*  test_fuzzy_search.cpp                                                 */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_fuzzy_search)

#include "core/io/file_access.h"
#include "core/string/fuzzy_search.h"
#include "tests/test_utils.h"

namespace TestFuzzySearch {

struct FuzzySearchTestCase {
	String query;
	String expected;
};

// Ideally each of these test queries should represent a different aspect, and potentially bottleneck, of the search process.
const FuzzySearchTestCase test_cases[] = {
	// Short query, many matches, few adjacent characters
	{ "///gd", "./menu/menu/characters/dead_guy/dead_guy.gd" },
	// Filename match with typo
	{ "sm.png", "./entity/blood_sword/sam.png" },
	// Multipart filename word matches
	{ "ham ", "./entity/game_trap/ha_missed_me.wav" },
	// Single word token matches
	{ "push background", "./entity/background_zone1/background/push.png" },
	// Long token matches
	{ "background_freighter background png", "./entity/background_freighter/background/background.png" },
	// Many matches, many short tokens
	{ "menu menu characters wav", "./menu/menu/characters/smoker/0.wav" },
	// Maximize total matches
	{ "entity gd", "./entity/entity_man.gd" },
	// End of filename bonus (vs ".menu/widgets/toggle.gd")
	{ "gg", "./entity/background_freighter/Master.ogg" },
	// Exact filename start beats exact word boundary in the filename of a shorter path
	{ "fallback", "./menu/fonts/fallback_font.ores" },
	// Incomplete exact word at filename start beats exact complete word later in filename
	{ "me", "./menu/metal_text.ores" },
	// Exact word boundary in middle of a filename beats exact dirname start
	{ "floor", "./entity/background_freighter/background/background_floor.png" },
	// Exact match beats inexact filename start match (vs "./entity/background_zone_2/background/wall_overlay.png")
	{ "wor", "./entity/blood_sword/data.gd" },
	// Match starting on filename beats match later in filename (vs "./menu/ui_colors.ores")
	{ "colo.ores", "./menu/menu/characters/color.ores" }
};

Vector<String> load_test_data() {
	Ref<FileAccess> fp = FileAccess::open(TestUtils::get_data_path("fuzzy_search/project_dir_tree.txt"), FileAccess::READ);
	REQUIRE(fp.is_valid());
	return fp->get_as_utf8_string().split("\n");
}

TEST_CASE("[FuzzySearch] Test fuzzy search results") {
	FuzzySearch search;
	Vector<String> targets = load_test_data();

	for (FuzzySearchTestCase test_case : test_cases) {
		Vector<Ref<FuzzySearchMatch>> results = search.search_all(test_case.query, targets);
		CHECK_GT(results.size(), 0);
		CHECK_EQ(results[0]->get_target(), test_case.expected);
	}
}

} // namespace TestFuzzySearch
