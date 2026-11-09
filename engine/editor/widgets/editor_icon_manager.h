/**************************************************************************/
/*  editor_icon_manager.h                                                 */

#pragma once

#include "scene/ui/control.h"

class AtlasTexture;
class Texture2D;

class EditorIconManager : public Control {
	ORBLIT_CLASS(EditorIconManager, Control);

	HashMap<StringName, Ref<AtlasTexture>> icons;

	inline static EditorIconManager *singleton = nullptr;

protected:
	void _notification(int p_what);

public:
	EditorIconManager();

	static Ref<Texture2D> get_icon(const StringName &p_icon_name);
};
