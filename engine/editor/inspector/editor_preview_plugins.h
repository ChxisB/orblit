/**************************************************************************/
/*  editor_preview_plugins.h                                              */

#pragma once

#include "core/io/image.h"
#include "editor/inspector/editor_resource_preview.h"

class ScriptLanguage;

void post_process_preview(Ref<Image> p_image);

class EditorTexturePreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorTexturePreviewPlugin, EditorResourcePreviewGenerator);

public:
	virtual bool handles(const String &p_type) const override;
	virtual bool generate_small_preview_automatically() const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
};

class EditorImagePreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorImagePreviewPlugin, EditorResourcePreviewGenerator);

public:
	virtual bool handles(const String &p_type) const override;
	virtual bool generate_small_preview_automatically() const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
};

class EditorBitmapPreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorBitmapPreviewPlugin, EditorResourcePreviewGenerator);

public:
	virtual bool handles(const String &p_type) const override;
	virtual bool generate_small_preview_automatically() const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
};

class EditorPrefabPreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorPrefabPreviewPlugin, EditorResourcePreviewGenerator);

public:
	virtual bool handles(const String &p_type) const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
	virtual Ref<Texture2D> generate_from_path(const String &p_path, const Size2 &p_size, Dictionary &p_metadata) const override;
};

class EditorMaterialPreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorMaterialPreviewPlugin, EditorResourcePreviewGenerator);

	RID scenario;
	RID sphere;
	RID sphere_instance;
	RID viewport;
	RID viewport_texture;
	RID light;
	RID light_instance;
	RID light2;
	RID light_instance2;
	RID camera;
	RID camera_attributes;
	mutable DrawRequester draw_requester;

public:
	virtual bool handles(const String &p_type) const override;
	virtual bool generate_small_preview_automatically() const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
	virtual void abort() override;

	EditorMaterialPreviewPlugin();
	~EditorMaterialPreviewPlugin();
};

class EditorScriptPreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorScriptPreviewPlugin, EditorResourcePreviewGenerator);

	Ref<Texture2D> _generate_from_source_code(const ScriptLanguage *p_language, const String &p_source_code, const Size2 &p_size, Dictionary &p_metadata) const;

public:
	virtual bool handles(const String &p_type) const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
	virtual Ref<Texture2D> generate_from_path(const String &p_path, const Size2 &p_size, Dictionary &p_metadata) const override;
};

class EditorAudioStreamPreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorAudioStreamPreviewPlugin, EditorResourcePreviewGenerator);

public:
	virtual bool handles(const String &p_type) const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
};

class EditorMeshPreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorMeshPreviewPlugin, EditorResourcePreviewGenerator);

	RID scenario;
	RID mesh_instance;
	RID viewport;
	RID viewport_texture;
	RID light;
	RID light_instance;
	RID light2;
	RID light_instance2;
	RID camera;
	RID camera_attributes;
	mutable DrawRequester draw_requester;

public:
	virtual bool handles(const String &p_type) const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
	virtual void abort() override;

	EditorMeshPreviewPlugin();
	~EditorMeshPreviewPlugin();
};

class EditorFontPreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorFontPreviewPlugin, EditorResourcePreviewGenerator);

	RID viewport;
	RID viewport_texture;
	RID canvas;
	RID canvas_item;
	mutable DrawRequester draw_requester;

public:
	virtual bool handles(const String &p_type) const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
	virtual Ref<Texture2D> generate_from_path(const String &p_path, const Size2 &p_size, Dictionary &p_metadata) const override;
	virtual void abort() override;

	EditorFontPreviewPlugin();
	~EditorFontPreviewPlugin();
};

class EditorGradientPreviewPlugin : public EditorResourcePreviewGenerator {
	ORBLIT_CLASS(EditorGradientPreviewPlugin, EditorResourcePreviewGenerator);

public:
	virtual bool handles(const String &p_type) const override;
	virtual bool generate_small_preview_automatically() const override;
	virtual Ref<Texture2D> generate(const Ref<Resource> &p_from, const Size2 &p_size, Dictionary &p_metadata) const override;
};
