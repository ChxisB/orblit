/**************************************************************************/
/*  shader_include.h                                                      */

#pragma once

#include "core/io/resource.h"
#include "core/containers/hash_set.h"

class ShaderInclude : public Resource {
	ORBLIT_CLASS(ShaderInclude, Resource);
	OBJ_SAVE_TYPE(ShaderInclude);

private:
	String code;
	String include_path;
	HashSet<Ref<ShaderInclude>> dependencies;
	void _dependency_changed();

protected:
	static void register_members();

public:
	void set_code(const String &p_text);
	String get_code() const;

	void set_include_path(const String &p_path);
};
