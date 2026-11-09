/**************************************************************************/
/*  shader.h                                                              */

#pragma once

#include "core/io/resource.h"
#include "scene/resources/shader_include.h"

class Texture;
class Texture2D;

class Shader : public Resource {
	ORBLIT_CLASS(Shader, Resource);
	OBJ_SAVE_TYPE(Shader);

public:
	// Must be kept in sync with the List<String> of shader types in `systems/rendering/shader_types.cpp`.
	enum Mode {
		MODE_SPATIAL,
		MODE_CANVAS_ITEM,
		MODE_PARTICLES,
		MODE_SKY,
		MODE_FOG,
		MODE_TEXTURE_BLIT,
		MODE_MAX
	};

private:
	mutable RID shader_rid;
	mutable String preprocessed_code;
	mutable Mutex shader_rid_mutex;

	Mode mode = MODE_SPATIAL;
	HashSet<Ref<ShaderInclude>> include_dependencies;
	String code;
	String include_path;

	HashMap<StringName, HashMap<int, Ref<Texture>>> default_textures;

	void _check_shader_rid() const;
	void _dependency_changed();
	void _recompile();
	virtual void _update_shader() const; //used for visual shader
	Array _get_shader_uniform_list(bool p_get_groups = false);

protected:

	static void register_members();

public:
	//void set_mode(Mode p_mode);
	virtual Mode get_mode() const;

	virtual void set_path(const String &p_path, bool p_take_over = false) override;
	void set_include_path(const String &p_path);

	void set_code(const String &p_code);
	String get_code() const;

	void inspect_native_shader_code();

	void get_shader_uniform_list(List<PropertyInfo> *p_params, bool p_get_groups = false) const;

	void set_default_texture_parameter(const StringName &p_name, const Ref<Texture> &p_texture, int p_index = 0);
	Ref<Texture> get_default_texture_parameter(const StringName &p_name, int p_index = 0) const;
	void get_default_texture_parameter_list(List<StringName> *r_textures) const;

	virtual bool is_text_shader() const;

	virtual RID get_rid() const override;

	Shader();
	~Shader();
};

VARIANT_ENUM_CAST(Shader::Mode);
