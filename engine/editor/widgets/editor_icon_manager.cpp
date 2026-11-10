/**************************************************************************/
/*  editor_icon_manager.cpp                                               */

#include "editor_icon_manager.h"

#include "editor/editor_node.h"
#include "scene/resources/atlas_texture.h"

void EditorIconManager::_notification(int p_what) {
	if (p_what == NOTIFICATION_THEME_CHANGED) {
		for (KeyValue<StringName, Ref<AtlasTexture>> &icon : icons) {
			icon.value->set_atlas(get_editor_theme_icon(icon.key));
		}
	}
}

EditorIconManager::EditorIconManager() {
	singleton = this;
}

Ref<Texture2D> EditorIconManager::get_icon(const StringName &p_icon_name) {
	Ref<AtlasTexture> *existing = singleton->icons.getptr(p_icon_name);
	if (existing) {
		return *existing;
	}
	Ref<AtlasTexture> icon;
	icon.instantiate();
	icon->set_atlas(singleton->get_editor_theme_icon(p_icon_name));
	singleton->icons[p_icon_name] = icon;
	return icon;
}
