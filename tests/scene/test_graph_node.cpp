/**************************************************************************/
/*  test_graph_node.cpp                                                   */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_graph_node)

#ifndef ADVANCED_GUI_DISABLED

#include "scene/ui/graph_node.h"

namespace TestGraphNode {

TEST_CASE("[GraphNode][Stage]") {
	SUBCASE("[GraphNode] Graph Entity only child on delete should not cause error.") {
		// Setup.
		GraphNode *test_node = memnew(GraphNode);
		test_node->set_name("Graph Entity");
		Control *test_child = memnew(Control);
		test_child->set_name("child");
		test_node->add_child(test_child);

		// Test.
		test_node->remove_child(test_child);
		CHECK(test_node->get_child_count(false) == 0);

		memdelete(test_child);
		memdelete(test_node);
	}
}

} // namespace TestGraphNode

#endif // ADVANCED_GUI_DISABLED
