/**************************************************************************/
/*  editor_resource_tooltip_plugins.h                                     */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"
#include "scene/ui/control.h"

class Texture2D;
class TextureRect;
class VBoxContainer;

class EditorResourceTooltipPlugin : public RefCounted {
	ORBLIT_CLASS(EditorResourceTooltipPlugin, RefCounted);

	void _thumbnail_ready(const String &p_path, const Ref<Texture2D> &p_preview, const Ref<Texture2D> &p_small_preview, ObjectID p_trect_id);

protected:
	static void register_members();

	ORBLIT_VIRTUAL1RC(bool, _handles, String)
	ORBLIT_VIRTUAL3RC(Control *, _make_tooltip_for_path, String, Dictionary, Control *)

public:
	static VBoxContainer *make_default_tooltip(const String &p_resource_path);
	void request_thumbnail(const String &p_path, TextureRect *p_for_control) const;

	virtual bool handles(const String &p_resource_type) const;
	virtual Control *make_tooltip_for_path(const String &p_resource_path, const Dictionary &p_metadata, Control *p_base) const;
};

class EditorTextureTooltipPlugin : public EditorResourceTooltipPlugin {
	ORBLIT_CLASS(EditorTextureTooltipPlugin, EditorResourceTooltipPlugin);

public:
	virtual bool handles(const String &p_resource_type) const override;
	virtual Control *make_tooltip_for_path(const String &p_resource_path, const Dictionary &p_metadata, Control *p_base) const override;
};

class EditorAudioStreamTooltipPlugin : public EditorResourceTooltipPlugin {
	ORBLIT_CLASS(EditorAudioStreamTooltipPlugin, EditorResourceTooltipPlugin);

public:
	virtual bool handles(const String &p_resource_type) const override;
	virtual Control *make_tooltip_for_path(const String &p_resource_path, const Dictionary &p_metadata, Control *p_base) const override;
};
