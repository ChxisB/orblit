/**************************************************************************/
/*  shader_include.cpp                                                    */

#include "shader_include.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "systems/rendering/shader_preprocessor.h"

void ShaderInclude::_dependency_changed() {
	emit_changed();
}

void ShaderInclude::set_code(const String &p_code) {
	code = p_code;

	for (const Ref<ShaderInclude> &E : dependencies) {
		E->disconnect_changed(callable_mp(this, &ShaderInclude::_dependency_changed));
	}

	{
		String path = get_path();
		if (path.is_empty()) {
			path = include_path;
		}

		String pp_code;
		HashSet<Ref<ShaderInclude>> new_dependencies;
		ShaderPreprocessor preprocessor;
		Error result = preprocessor.preprocess(p_code, path, pp_code, &new_dependencies);
		if (result == OK) {
			// This ensures previous include resources are not freed and then re-loaded during parse (which would make compiling slower)
			dependencies = new_dependencies;
		}
	}

	for (const Ref<ShaderInclude> &E : dependencies) {
		E->connect_changed(callable_mp(this, &ShaderInclude::_dependency_changed));
	}

	emit_changed();
}

String ShaderInclude::get_code() const {
	return code;
}

void ShaderInclude::set_include_path(const String &p_path) {
	include_path = p_path;
}

void ShaderInclude::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_code", "code"), &ShaderInclude::set_code);
	ClassRegistry::bind_method(D_METHOD("get_code"), &ShaderInclude::get_code);

	ADD_PROPERTY(PropertyInfo(Variant::STRING, "code", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_code", "get_code");
}
