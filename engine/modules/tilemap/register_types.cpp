/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "tile_map_layer.h"
#include "tile_set.h"

#ifdef TOOLS_ENABLED
#include "editor/tiles_editor_plugin.h"
#endif

#include "core/object/class_registry.h"

void initialize_tilemap_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(TileSet);
		ORBLIT_REGISTER_ABSTRACT_CLASS(TileSetSource);
		ORBLIT_REGISTER_CLASS(TileSetAtlasSource);
		ORBLIT_REGISTER_CLASS(TileSetScenesCollectionSource);
		ORBLIT_REGISTER_CLASS(TileMapPattern);
		ORBLIT_REGISTER_CLASS(TileData);
		ORBLIT_REGISTER_CLASS(TileMapLayer);

#ifndef NAVIGATION_PLANAR_DISABLED
		TileMapLayer::navmesh_parse_init();
#endif // NAVIGATION_PLANAR_DISABLED
	}
#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		EditorPlugins::add_by_type<TileSetEditorPlugin>();
		EditorPlugins::add_by_type<TileMapEditorPlugin>();
	}
#endif
}

void uninitialize_tilemap_module(ModuleInitializationLevel p_level) {}
