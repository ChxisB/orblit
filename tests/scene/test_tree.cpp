/**************************************************************************/
/*  test_tree.cpp                                                         */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_tree)

#ifndef ADVANCED_GUI_DISABLED

#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/tree.h"

namespace TestTree {

TEST_CASE("[Stage][Tree]") {
	SUBCASE("[Tree] Create and remove items.") {
		Tree *tree = memnew(Tree);
		TreeItem *root = tree->create_item();

		TreeItem *child1 = tree->create_item();
		CHECK_EQ(root->get_child_count(), 1);

		TreeItem *child2 = tree->create_item(root);
		CHECK_EQ(root->get_child_count(), 2);

		TreeItem *child3 = tree->create_item(root, 0);
		CHECK_EQ(root->get_child_count(), 3);

		CHECK_EQ(root->get_child(0), child3);
		CHECK_EQ(root->get_child(1), child1);
		CHECK_EQ(root->get_child(2), child2);

		root->remove_child(child3);
		CHECK_EQ(root->get_child_count(), 2);

		root->add_child(child3);
		CHECK_EQ(root->get_child_count(), 3);

		TreeItem *child4 = root->create_child();
		CHECK_EQ(root->get_child_count(), 4);

		CHECK_EQ(root->get_child(0), child1);
		CHECK_EQ(root->get_child(1), child2);
		CHECK_EQ(root->get_child(2), child3);
		CHECK_EQ(root->get_child(3), child4);

		memdelete(tree);
	}

	SUBCASE("[Tree] Clear items.") {
		Tree *tree = memnew(Tree);
		TreeItem *root = tree->create_item();

		for (int i = 0; i < 10; i++) {
			tree->create_item();
		}
		CHECK_EQ(root->get_child_count(), 10);

		root->clear_children();
		CHECK_EQ(root->get_child_count(), 0);

		memdelete(tree);
	}

	SUBCASE("[Tree] Get last item.") {
		Tree *tree = memnew(Tree);
		TreeItem *root = tree->create_item();

		TreeItem *last;
		for (int i = 0; i < 10; i++) {
			last = tree->create_item();
		}
		CHECK_EQ(root->get_child_count(), 10);
		CHECK_EQ(tree->get_last_item(), last);

		// Check nested.
		TreeItem *old_last = last;
		for (int i = 0; i < 10; i++) {
			last = tree->create_item(old_last);
		}
		CHECK_EQ(tree->get_last_item(), last);

		memdelete(tree);
	}

	SUBCASE("[Tree] Get last item after removal.") {
		Tree *tree = memnew(Tree);
		TreeItem *root = tree->create_item();

		TreeItem *child1 = tree->create_item(root);
		TreeItem *child2 = tree->create_item(root);

		CHECK_EQ(root->get_child_count(), 2);
		CHECK_EQ(tree->get_last_item(), child2);

		root->remove_child(child2);

		CHECK_EQ(root->get_child_count(), 1);
		CHECK_EQ(tree->get_last_item(), child1);

		root->add_child(child2);

		CHECK_EQ(root->get_child_count(), 2);
		CHECK_EQ(tree->get_last_item(), child2);

		memdelete(tree);
	}

	SUBCASE("[Tree] Create items after moving items.") {
		Tree *tree = memnew(Tree);
		TreeItem *root = tree->create_item();

		TreeItem *child1 = tree->create_item(root);
		TreeItem *child2 = tree->create_item(root);
		TreeItem *child3 = tree->create_item(root);

		child3->move_before(child2);
		TreeItem *child4 = tree->create_item(root, 0);
		TreeItem *child5 = tree->create_item(root, 2);

		CHECK_EQ(root->get_child_count(), 5);
		CHECK_EQ(root->get_child(0), child4);
		CHECK_EQ(root->get_child(1), child1);
		CHECK_EQ(root->get_child(2), child5);
		CHECK_EQ(root->get_child(3), child3);
		CHECK_EQ(root->get_child(4), child2);

		memdelete(tree);
	}

	SUBCASE("[Tree] Previous and Next items.") {
		Tree *tree = memnew(Tree);
		TreeItem *root = tree->create_item();

		TreeItem *child1 = tree->create_item();
		TreeItem *child2 = tree->create_item();
		TreeItem *child3 = tree->create_item();
		CHECK_EQ(root->get_next(), nullptr);
		CHECK_EQ(root->get_next_visible(), child1);
		CHECK_EQ(root->get_next_in_tree(), child1);
		CHECK_EQ(child1->get_next(), child2);
		CHECK_EQ(child1->get_next_visible(), child2);
		CHECK_EQ(child1->get_next_in_tree(), child2);
		CHECK_EQ(child2->get_next(), child3);
		CHECK_EQ(child2->get_next_visible(), child3);
		CHECK_EQ(child2->get_next_in_tree(), child3);
		CHECK_EQ(child3->get_next(), nullptr);
		CHECK_EQ(child3->get_next_visible(), nullptr);
		CHECK_EQ(child3->get_next_in_tree(), nullptr);

		CHECK_EQ(root->get_prev(), nullptr);
		CHECK_EQ(root->get_prev_visible(), nullptr);
		CHECK_EQ(root->get_prev_in_tree(), nullptr);
		CHECK_EQ(child1->get_prev(), nullptr);
		CHECK_EQ(child1->get_prev_visible(), root);
		CHECK_EQ(child1->get_prev_in_tree(), root);
		CHECK_EQ(child2->get_prev(), child1);
		CHECK_EQ(child2->get_prev_visible(), child1);
		CHECK_EQ(child2->get_prev_in_tree(), child1);
		CHECK_EQ(child3->get_prev(), child2);
		CHECK_EQ(child3->get_prev_visible(), child2);
		CHECK_EQ(child3->get_prev_in_tree(), child2);

		TreeItem *nested1 = tree->create_item(child2);
		TreeItem *nested2 = tree->create_item(child2);
		TreeItem *nested3 = tree->create_item(child2);

		CHECK_EQ(child1->get_next(), child2);
		CHECK_EQ(child1->get_next_visible(), child2);
		CHECK_EQ(child1->get_next_in_tree(), child2);
		CHECK_EQ(child2->get_next(), child3);
		CHECK_EQ(child2->get_next_visible(), nested1);
		CHECK_EQ(child2->get_next_in_tree(), nested1);
		CHECK_EQ(child3->get_prev(), child2);
		CHECK_EQ(child3->get_prev_visible(), nested3);
		CHECK_EQ(child3->get_prev_in_tree(), nested3);
		CHECK_EQ(nested1->get_prev_in_tree(), child2);
		CHECK_EQ(nested1->get_next_in_tree(), nested2);
		CHECK_EQ(nested3->get_next_in_tree(), child3);

		memdelete(tree);
	}

	SUBCASE("[Tree] Previous and Next items with hide root.") {
		Tree *tree = memnew(Tree);
		tree->set_hide_root(true);
		TreeItem *root = tree->create_item();

		TreeItem *child1 = tree->create_item();
		TreeItem *child2 = tree->create_item();
		TreeItem *child3 = tree->create_item();
		CHECK_EQ(root->get_next(), nullptr);
		CHECK_EQ(root->get_next_visible(), child1);
		CHECK_EQ(root->get_next_in_tree(), child1);
		CHECK_EQ(child1->get_next(), child2);
		CHECK_EQ(child1->get_next_visible(), child2);
		CHECK_EQ(child1->get_next_in_tree(), child2);
		CHECK_EQ(child2->get_next(), child3);
		CHECK_EQ(child2->get_next_visible(), child3);
		CHECK_EQ(child2->get_next_in_tree(), child3);
		CHECK_EQ(child3->get_next(), nullptr);
		CHECK_EQ(child3->get_next_visible(), nullptr);
		CHECK_EQ(child3->get_next_in_tree(), nullptr);

		CHECK_EQ(root->get_prev(), nullptr);
		CHECK_EQ(root->get_prev_visible(), nullptr);
		CHECK_EQ(root->get_prev_in_tree(), nullptr);
		CHECK_EQ(child1->get_prev(), nullptr);
		CHECK_EQ(child1->get_prev_visible(), nullptr);
		CHECK_EQ(child1->get_prev_in_tree(), nullptr);
		CHECK_EQ(child2->get_prev(), child1);
		CHECK_EQ(child2->get_prev_visible(), child1);
		CHECK_EQ(child2->get_prev_in_tree(), child1);
		CHECK_EQ(child3->get_prev(), child2);
		CHECK_EQ(child3->get_prev_visible(), child2);
		CHECK_EQ(child3->get_prev_in_tree(), child2);

		memdelete(tree);
	}

	SUBCASE("[Tree] Previous and Next items wrapping.") {
		Tree *tree = memnew(Tree);
		TreeItem *root = tree->create_item();

		TreeItem *child1 = tree->create_item();
		TreeItem *child2 = tree->create_item();
		TreeItem *child3 = tree->create_item();
		CHECK_EQ(root->get_next_visible(true), child1);
		CHECK_EQ(root->get_next_in_tree(true), child1);
		CHECK_EQ(child1->get_next_visible(true), child2);
		CHECK_EQ(child1->get_next_in_tree(true), child2);
		CHECK_EQ(child2->get_next_visible(true), child3);
		CHECK_EQ(child2->get_next_in_tree(true), child3);
		CHECK_EQ(child3->get_next_visible(true), root);
		CHECK_EQ(child3->get_next_in_tree(true), root);

		CHECK_EQ(root->get_prev_visible(true), child3);
		CHECK_EQ(root->get_prev_in_tree(true), child3);
		CHECK_EQ(child1->get_prev_visible(true), root);
		CHECK_EQ(child1->get_prev_in_tree(true), root);
		CHECK_EQ(child2->get_prev_visible(true), child1);
		CHECK_EQ(child2->get_prev_in_tree(true), child1);
		CHECK_EQ(child3->get_prev_visible(true), child2);
		CHECK_EQ(child3->get_prev_in_tree(true), child2);

		TreeItem *nested1 = tree->create_item(child2);
		TreeItem *nested2 = tree->create_item(child2);
		TreeItem *nested3 = tree->create_item(child2);

		CHECK_EQ(child1->get_next_visible(true), child2);
		CHECK_EQ(child1->get_next_in_tree(true), child2);
		CHECK_EQ(child2->get_next_visible(true), nested1);
		CHECK_EQ(child2->get_next_in_tree(true), nested1);
		CHECK_EQ(nested3->get_next_visible(true), child3);
		CHECK_EQ(nested3->get_next_in_tree(true), child3);
		CHECK_EQ(child3->get_prev_visible(true), nested3);
		CHECK_EQ(child3->get_prev_in_tree(true), nested3);
		CHECK_EQ(nested1->get_prev_in_tree(true), child2);
		CHECK_EQ(nested1->get_next_in_tree(true), nested2);
		CHECK_EQ(nested3->get_next_in_tree(true), child3);

		memdelete(tree);
	}

	SUBCASE("[Tree] Previous and Next items wrapping with hide root.") {
		Tree *tree = memnew(Tree);
		tree->set_hide_root(true);
		TreeItem *root = tree->create_item();

		TreeItem *child1 = tree->create_item();
		TreeItem *child2 = tree->create_item();
		TreeItem *child3 = tree->create_item();
		CHECK_EQ(root->get_next_visible(true), child1);
		CHECK_EQ(root->get_next_in_tree(true), child1);
		CHECK_EQ(child1->get_next_visible(true), child2);
		CHECK_EQ(child1->get_next_in_tree(true), child2);
		CHECK_EQ(child2->get_next_visible(true), child3);
		CHECK_EQ(child2->get_next_in_tree(true), child3);
		CHECK_EQ(child3->get_next_visible(true), root);
		CHECK_EQ(child3->get_next_in_tree(true), root);

		CHECK_EQ(root->get_prev_visible(true), child3);
		CHECK_EQ(root->get_prev_in_tree(true), child3);
		CHECK_EQ(child1->get_prev_visible(true), child3);
		CHECK_EQ(child1->get_prev_in_tree(true), child3);
		CHECK_EQ(child2->get_prev_visible(true), child1);
		CHECK_EQ(child2->get_prev_in_tree(true), child1);
		CHECK_EQ(child3->get_prev_visible(true), child2);
		CHECK_EQ(child3->get_prev_in_tree(true), child2);

		memdelete(tree);
	}
}

TEST_CASE("[Stage][Tree] drop section beside a child of a hidden root") {
	Tree *tree = memnew(Tree);
	Stage::get_singleton()->get_root()->add_child(tree);
	tree->set_size(Size2(300, 300));
	tree->set_hide_root(true);
	tree->set_drop_mode_flags(Tree::DROP_MODE_ON_ITEM | Tree::DROP_MODE_INBETWEEN);
	TreeItem *root = tree->create_item();
	TreeItem *first = tree->create_item(root);
	tree->create_item(root);
	MessageQueue::get_singleton()->flush();

	// A point in the indent space, left of the first item content.
	const Rect2 rect = tree->get_item_rect(first);
	const Point2 top_half = rect.position + Point2(1, 2);
	const Point2 bottom_half = rect.position + Point2(1, rect.size.height - 2);

	// The hidden root has no row, so its children can take a sibling above.
	CHECK(tree->get_drop_section_at_position(top_half) == -1);
	CHECK(tree->get_drop_section_at_position(bottom_half) == 1);

	memdelete(tree);
}

} // namespace TestTree

#endif // ADVANCED_GUI_DISABLED
