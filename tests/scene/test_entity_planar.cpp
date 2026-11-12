/**************************************************************************/
/*  test_entity_planar.cpp                                                    */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_entity_planar)

#include "core/object/callable_mp.h"
#include "core/object/message_queue.h"
#include "scene/planar/entity_planar.h"
#include "scene/planar/remote_transform_planar.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "systems/text/text_system.h"

namespace TestEntity2D {

class OversamplingProbe : public Entity2D {
	ORBLIT_CLASS(OversamplingProbe, Entity2D);

public:
	double seen = -1.0;

	void record() {
		seen = TextSystem::get_current_drawn_item_oversampling();
	}
};

TEST_CASE("[Stage][Entity2D]") {
	SUBCASE("[Entity2D][Global Transform] Global Transform should be accessible while not in Stage.") { // GH-79453
		Entity2D *test_node = memnew(Entity2D);
		test_node->set_name("node");
		Entity2D *test_child = memnew(Entity2D);
		test_child->set_name("child");
		test_node->add_child(test_child);

		test_node->set_global_position(Point2(1, 1));
		CHECK_EQ(test_node->get_global_position(), Point2(1, 1));
		CHECK_EQ(test_child->get_global_position(), Point2(1, 1));
		test_node->set_global_position(Point2(2, 2));
		CHECK_EQ(test_node->get_global_position(), Point2(2, 2));
		test_node->set_global_transform(Transform2D(0, Point2(3, 3)));
		CHECK_EQ(test_node->get_global_position(), Point2(3, 3));

		memdelete(test_child);
		memdelete(test_node);
	}

	SUBCASE("[Entity2D][Global Transform] Global Transform should be correct after inserting node from detached tree into Stage.") { // GH-86841
		Entity2D *main = memnew(Entity2D);
		Entity2D *outer = memnew(Entity2D);
		Entity2D *inner = memnew(Entity2D);
		Stage::get_singleton()->get_root()->add_child(main);

		main->set_position(Point2(100, 100));
		outer->set_position(Point2(10, 0));
		inner->set_position(Point2(0, 10));

		outer->add_child(inner);
		// `inner` is still detached.
		CHECK_EQ(inner->get_global_position(), Point2(10, 10));

		main->add_child(outer);
		// `inner` is in scene tree.
		CHECK_EQ(inner->get_global_position(), Point2(110, 110));

		main->remove_child(outer);
		// `inner` is detached again.
		CHECK_EQ(inner->get_global_position(), Point2(10, 10));

		memdelete(inner);
		memdelete(outer);
		memdelete(main);
	}
}

TEST_CASE("[Stage][Entity2D] Utility methods") {
	Entity2D *test_node1 = memnew(Entity2D);
	Entity2D *test_node2 = memnew(Entity2D);
	Entity2D *test_node3 = memnew(Entity2D);
	Entity2D *test_sibling = memnew(Entity2D);
	Stage::get_singleton()->get_root()->add_child(test_node1);

	test_node1->set_position(Point2(100, 100));
	test_node1->set_rotation(Math::deg_to_rad(30.0));
	test_node1->set_scale(Size2(1, 1));
	test_node1->add_child(test_node2);

	test_node2->set_position(Point2(10, 0));
	test_node2->set_rotation(Math::deg_to_rad(60.0));
	test_node2->set_scale(Size2(1, 1));
	test_node2->add_child(test_node3);
	test_node2->add_child(test_sibling);

	test_node3->set_position(Point2(0, 10));
	test_node3->set_rotation(Math::deg_to_rad(0.0));
	test_node3->set_scale(Size2(2, 2));

	test_sibling->set_position(Point2(5, 10));
	test_sibling->set_rotation(Math::deg_to_rad(90.0));
	test_sibling->set_scale(Size2(2, 1));

	SUBCASE("[Entity2D] look_at") {
		test_node3->look_at(Vector2(1, 1));

		CHECK(test_node3->get_global_position().is_equal_approx(Point2(98.66026, 105)));
		CHECK(Math::is_equal_approx(test_node3->get_global_rotation(), real_t(-2.32477)));
		CHECK(test_node3->get_global_scale().is_equal_approx(Vector2(2, 2)));

		CHECK(test_node3->get_position().is_equal_approx(Vector2(0, 10)));
		CHECK(Math::is_equal_approx(test_node3->get_rotation(), real_t(2.38762)));
		CHECK(test_node3->get_scale().is_equal_approx(Vector2(2, 2)));

		test_node3->look_at(Vector2(0, 10));

		CHECK(test_node3->get_global_position().is_equal_approx(Vector2(98.66026, 105)));
		CHECK(Math::is_equal_approx(test_node3->get_global_rotation(), real_t(-2.37509)));
		CHECK(test_node3->get_global_scale().is_equal_approx(Vector2(2, 2)));

		CHECK(test_node3->get_position().is_equal_approx(Vector2(0, 10)));
		CHECK(Math::is_equal_approx(test_node3->get_rotation(), real_t(2.3373)));
		CHECK(test_node3->get_scale().is_equal_approx(Vector2(2, 2)));

		// Don't do anything if look_at own position.
		test_node3->look_at(test_node3->get_global_position());

		CHECK(test_node3->get_global_position().is_equal_approx(Vector2(98.66026, 105)));
		CHECK(Math::is_equal_approx(test_node3->get_global_rotation(), real_t(-2.37509)));
		CHECK(test_node3->get_global_scale().is_equal_approx(Vector2(2, 2)));

		CHECK(test_node3->get_position().is_equal_approx(Vector2(0, 10)));
		CHECK(Math::is_equal_approx(test_node3->get_rotation(), real_t(2.3373)));
		CHECK(test_node3->get_scale().is_equal_approx(Vector2(2, 2)));

		// Revert any rotation caused by look_at, must run after look_at tests
		test_node3->set_rotation(Math::deg_to_rad(0.0));
	}

	SUBCASE("[Entity2D] get_angle_to") {
		CHECK(Math::is_equal_approx(test_node3->get_angle_to(Vector2(1, 1)), real_t(2.38762)));
		CHECK(Math::is_equal_approx(test_node3->get_angle_to(Vector2(0, 10)), real_t(2.3373)));
		CHECK(Math::is_equal_approx(test_node3->get_angle_to(Vector2(2, -5)), real_t(2.42065)));
		CHECK(Math::is_equal_approx(test_node3->get_angle_to(Vector2(-2, 5)), real_t(2.3529)));

		// Return 0 when get_angle_to own position.
		CHECK(Math::is_equal_approx(test_node3->get_angle_to(test_node3->get_global_position()), real_t(0)));
	}

	SUBCASE("[Entity2D] to_local") {
		Point2 node3_local = test_node3->to_local(Point2(1, 2));
		CHECK(node3_local.is_equal_approx(Point2(-51.5, 48.83013)));

		node3_local = test_node3->to_local(Point2(-2, 1));
		CHECK(node3_local.is_equal_approx(Point2(-52, 50.33013)));

		node3_local = test_node3->to_local(Point2(0, 0));
		CHECK(node3_local.is_equal_approx(Point2(-52.5, 49.33013)));

		node3_local = test_node3->to_local(test_node3->get_global_position());
		CHECK(node3_local.is_equal_approx(Point2(0, 0)));
	}

	SUBCASE("[Entity2D] to_global") {
		Point2 node3_global = test_node3->to_global(Point2(1, 2));
		CHECK(node3_global.is_equal_approx(Point2(94.66026, 107)));

		node3_global = test_node3->to_global(Point2(-2, 1));
		CHECK(node3_global.is_equal_approx(Point2(96.66026, 101)));

		node3_global = test_node3->to_global(Point2(0, 0));
		CHECK(node3_global.is_equal_approx(test_node3->get_global_position()));
	}

	SUBCASE("[Entity2D] get_relative_transform_to_parent") {
		Transform2D relative_xform = test_node3->get_relative_transform_to_parent(test_node3);
		CHECK(relative_xform.is_equal_approx(Transform2D()));

		relative_xform = test_node3->get_relative_transform_to_parent(test_node2);
		CHECK(relative_xform.get_origin().is_equal_approx(Vector2(0, 10)));
		CHECK(Math::is_equal_approx(relative_xform.get_rotation(), real_t(0)));
		CHECK(relative_xform.get_scale().is_equal_approx(Vector2(2, 2)));

		relative_xform = test_node3->get_relative_transform_to_parent(test_node1);
		CHECK(relative_xform.get_origin().is_equal_approx(Vector2(1.339746, 5)));
		CHECK(Math::is_equal_approx(relative_xform.get_rotation(), real_t(1.0472)));
		CHECK(relative_xform.get_scale().is_equal_approx(Vector2(2, 2)));

		ERR_PRINT_OFF;
		// In case of a sibling all transforms until the root are accumulated.
		Transform2D xform = test_node3->get_relative_transform_to_parent(test_sibling);
		Transform2D return_xform = test_node1->get_global_transform().inverse() * test_node3->get_global_transform();
		CHECK(xform.is_equal_approx(return_xform));
		ERR_PRINT_ON;
	}

	memdelete(test_sibling);
	memdelete(test_node3);
	memdelete(test_node2);
	memdelete(test_node1);
}

TEST_CASE("[Stage][Entity2D] oversampling follows a scale change of the parent") {
	OversamplingProbe *parent = memnew(OversamplingProbe);
	OversamplingProbe *child = memnew(OversamplingProbe);
	parent->add_child(child);
	Stage::get_singleton()->get_root()->add_child(parent);
	parent->connect("draw", callable_mp(parent, &OversamplingProbe::record));
	child->connect("draw", callable_mp(child, &OversamplingProbe::record));
	parent->set_oversampling_with_scale(CanvasItem::OVERSAMPLING_WITH_SCALE_ENABLED);
	MessageQueue::get_singleton()->flush();
	const double parent_before = parent->seen;
	const double child_before = child->seen;
	REQUIRE(parent_before > 0.0);
	REQUIRE(child_before > 0.0);

	parent->set_scale(Size2(3, 3));
	MessageQueue::get_singleton()->flush();

	CHECK(parent->seen == doctest::Approx(3.0 * parent_before));
	CHECK(child->seen == doctest::Approx(3.0 * child_before));

	memdelete(parent);
}

TEST_CASE("[Stage][RemoteTransform2D] A scale that is not updated does not drift") {
	Entity2D *parent = memnew(Entity2D);
	RemoteTransform2D *remote = memnew(RemoteTransform2D);
	Entity2D *target = memnew(Entity2D);
	Stage::get_singleton()->get_root()->add_child(parent);
	Stage::get_singleton()->get_root()->add_child(target);
	parent->add_child(remote);
	parent->set_scale(Size2(0.9, 1.1));
	parent->set_rotation(0.37);
	parent->set_skew(0.05);
	remote->set_update_scale(false);
	remote->set_remote_node(remote->get_path_to(target));

	for (int i = 0; i < 5000; i++) {
		remote->set_rotation(0.0137 * i);
		remote->set_position(Point2(i, 2 * i));
		Stage::get_singleton()->flush_transform_notifications();
	}

	Size2 scale = target->get_global_scale();
	CHECK(Math::abs(scale.x - 1) < 1e-6);
	CHECK(Math::abs(scale.y - 1) < 1e-6);

	// A scale set from outside is a new value to hold, not an error to correct.
	target->set_global_scale(Size2(2, 3));
	remote->set_position(Point2(1, 1));
	Stage::get_singleton()->flush_transform_notifications();
	scale = target->get_global_scale();
	CHECK(Math::abs(scale.x - 2) < 1e-5);
	CHECK(Math::abs(scale.y - 3) < 1e-5);

	memdelete(target);
	memdelete(parent);
}

} // namespace TestEntity2D
