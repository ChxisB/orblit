/**************************************************************************/
/*  post_import_plugin_skeleton_rest_fixer.h                              */

#pragma once

#include "editor/import/spatial/resource_importer_scene.h"

class PostImportPluginSkeletonRestFixer : public EditorScenePostImportPlugin {
	ORBLIT_CLASS(PostImportPluginSkeletonRestFixer, EditorScenePostImportPlugin);

public:
	virtual void get_internal_import_options(InternalImportCategory p_category, List<ResourceImporter::ImportOption> *r_options) override;
	virtual Variant get_internal_option_visibility(InternalImportCategory p_category, const String &p_scene_import_type, const String &p_option, const HashMap<StringName, Variant> &p_options) const override;
	virtual void internal_process(InternalImportCategory p_category, Entity *p_base_scene, Entity *p_node, Ref<Resource> p_resource, const Dictionary &p_options) override;
};
