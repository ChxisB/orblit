/**************************************************************************/
/*  test_skeleton_spatial.cpp                                                  */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_skeleton_spatial)

#ifndef SPATIAL_DISABLED

#include "scene/spatial/skeleton_spatial.h"

namespace TestSkeleton3D {

TEST_CASE("[Skeleton3D] Test per-bone meta") {
	Skeleton3D *skeleton = memnew(Skeleton3D);
	skeleton->add_bone("root");
	skeleton->set_bone_rest(0, Transform3D());

	// Adding meta to bone.
	skeleton->set_bone_meta(0, "key1", "value1");
	skeleton->set_bone_meta(0, "key2", 12345);
	CHECK_MESSAGE(skeleton->get_bone_meta(0, "key1") == "value1", "Bone meta missing.");
	CHECK_MESSAGE(skeleton->get_bone_meta(0, "key2") == Variant(12345), "Bone meta missing.");

	// Rename bone and check if meta persists.
	skeleton->set_bone_name(0, "renamed_root");
	CHECK_MESSAGE(skeleton->get_bone_meta(0, "key1") == "value1", "Bone meta missing.");
	CHECK_MESSAGE(skeleton->get_bone_meta(0, "key2") == Variant(12345), "Bone meta missing.");

	// Retrieve list of keys.
	List<StringName> keys;
	skeleton->get_bone_meta_list(0, &keys);
	CHECK_MESSAGE(keys.size() == 2, "Wrong number of bone meta keys.");
	CHECK_MESSAGE(keys.find("key1"), "key1 not found in bone meta list");
	CHECK_MESSAGE(keys.find("key2"), "key2 not found in bone meta list");

	// Removing meta.
	skeleton->set_bone_meta(0, "key1", Variant());
	skeleton->set_bone_meta(0, "key2", Variant());
	CHECK_MESSAGE(!skeleton->has_bone_meta(0, "key1"), "Bone meta key1 should be deleted.");
	CHECK_MESSAGE(!skeleton->has_bone_meta(0, "key2"), "Bone meta key2 should be deleted.");
	List<StringName> should_be_empty_keys;
	skeleton->get_bone_meta_list(0, &should_be_empty_keys);
	CHECK_MESSAGE(should_be_empty_keys.size() == 0, "Wrong number of bone meta keys.");

	// Deleting non-existing key should succeed.
	skeleton->set_bone_meta(0, "non-existing-key", Variant());
	memdelete(skeleton);
}

} // namespace TestSkeleton3D

#endif // SPATIAL_DISABLED
