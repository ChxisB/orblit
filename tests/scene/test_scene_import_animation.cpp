/**************************************************************************/
/*  test_scene_import_animation.cpp                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_scene_import_animation)

#ifdef TOOLS_ENABLED

#include "editor/import/spatial/resource_importer_scene.h"
#include "scene/animation/animation_player.h"
#include "scene/resources/animation.h"
#include "scene/spatial/entity_spatial.h"
#include "tests/test_tools.h"

namespace TestSceneImportAnimation {

TEST_CASE("[Stage][SceneImport] A track for a removed node is dropped without an error") {
	Entity3D *root = memnew(Entity3D);
	root->set_name("Root");
	Entity3D *kept = memnew(Entity3D);
	kept->set_name("Kept");
	root->add_child(kept);
	AnimationPlayer *player = memnew(AnimationPlayer);
	player->set_name("Player");
	root->add_child(player);

	Ref<Animation> animation;
	animation.instantiate();
	animation->set_length(1.0);
	const int kept_track = animation->add_track(Animation::TYPE_POSITION_3D);
	animation->track_set_path(kept_track, EntityPath("Kept"));
	animation->position_track_insert_key(kept_track, 0.0, Vector3(5, 5, 5));
	const int removed_track = animation->add_track(Animation::TYPE_POSITION_3D);
	animation->track_set_path(removed_track, EntityPath("Removed"));
	animation->position_track_insert_key(removed_track, 0.0, Vector3(5, 5, 5));
	Ref<AnimationLibrary> library;
	library.instantiate();
	library->add_animation("move", animation);
	player->add_animation_library("", library);

	Ref<ResourceImporterScene> importer;
	importer.instantiate("Prefab");
	ErrorDetector detector;
	importer->_post_fix_animations(root, root, Dictionary(), Dictionary(), 30.0f, true);

	CHECK_FALSE(detector.has_error);
	REQUIRE(animation->get_track_count() == 1);
	CHECK(animation->track_get_path(0) == EntityPath("Kept"));

	memdelete(root);
}

} // namespace TestSceneImportAnimation

#endif // TOOLS_ENABLED
