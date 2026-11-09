/**************************************************************************/
/*  post_import_plugin_skeleton_track_organizer.h                         */

#pragma once

#include "editor/import/spatial/resource_importer_scene.h"

class PostImportPluginSkeletonTrackOrganizer : public EditorScenePostImportPlugin {
	ORBLIT_CLASS(PostImportPluginSkeletonTrackOrganizer, EditorScenePostImportPlugin);

public:
	virtual void get_internal_import_options(InternalImportCategory p_category, List<ResourceImporter::ImportOption> *r_options) override;
	virtual void internal_process(InternalImportCategory p_category, Entity *p_base_scene, Entity *p_node, Ref<Resource> p_resource, const Dictionary &p_options) override;
};
