/**************************************************************************/
/*  shader_types.h                                                        */

#pragma once

#include "systems/rendering/rendering_system_enums.h"
#include "systems/rendering/shader_language.h"

class ShaderTypes {
	struct Type {
		HashMap<StringName, ShaderLanguage::FunctionInfo> functions;
		Vector<ShaderLanguage::ModeInfo> modes;
		Vector<ShaderLanguage::ModeInfo> stencil_modes;
	};

	HashMap<RSE::ShaderMode, Type> shader_modes;

	static ShaderTypes *singleton;

	HashSet<String> shader_types;
	List<String> shader_types_list;

public:
	static ShaderTypes *get_singleton() { return singleton; }

	const HashMap<StringName, ShaderLanguage::FunctionInfo> &get_functions(RSE::ShaderMode p_mode) const;
	const Vector<ShaderLanguage::ModeInfo> &get_modes(RSE::ShaderMode p_mode) const;
	const Vector<ShaderLanguage::ModeInfo> &get_stencil_modes(RSE::ShaderMode p_mode) const;
	const HashSet<String> &get_types() const;
	const List<String> &get_types_list() const;

	ShaderTypes();
};
