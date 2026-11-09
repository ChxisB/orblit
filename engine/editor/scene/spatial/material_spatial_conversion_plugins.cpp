/**************************************************************************/
/*  material_spatial_conversion_plugins.cpp                                    */

#include "material_spatial_conversion_plugins.h"

#include "editor/scene/material_editor_plugin.h"
#include "scene/resources/spatial/fog_material.h"
#include "scene/resources/spatial/sky_material.h"
#include "systems/rendering/rendering_system.h"

String StandardMaterial3DConversionPlugin::converts_to() const {
	return "ShaderMaterial";
}

bool StandardMaterial3DConversionPlugin::handles(const Ref<Resource> &p_resource) const {
	Ref<StandardMaterial3D> mat = p_resource;
	return mat.is_valid();
}

Ref<Resource> StandardMaterial3DConversionPlugin::convert(const Ref<Resource> &p_resource) const {
	Ref<StandardMaterial3D> mat = p_resource;
	Ref<ShaderMaterial> smat = MaterialEditor::make_shader_material(mat, false);
	if (smat.is_null()) {
		return smat;
	}

	List<PropertyInfo> params;
	RS::get_singleton()->get_shader_parameter_list(mat->get_shader_rid(), &params);

	for (const PropertyInfo &E : params) {
		// Texture parameter has to be treated specially since StandardMaterial3D saved it
		// as RID but ShaderMaterial needs Texture itself
		Ref<Texture2D> texture = mat->get_texture_by_name(E.name);
		if (texture.is_valid()) {
			smat->set_shader_parameter(E.name, texture);
		} else {
			Variant value = RS::get_singleton()->material_get_param(mat->get_rid(), E.name);
			smat->set_shader_parameter(E.name, value);
		}
	}
	return smat;
}

String ORMMaterial3DConversionPlugin::converts_to() const {
	return "ShaderMaterial";
}

bool ORMMaterial3DConversionPlugin::handles(const Ref<Resource> &p_resource) const {
	Ref<ORMMaterial3D> mat = p_resource;
	return mat.is_valid();
}

Ref<Resource> ORMMaterial3DConversionPlugin::convert(const Ref<Resource> &p_resource) const {
	Ref<ORMMaterial3D> mat = p_resource;
	Ref<ShaderMaterial> smat = MaterialEditor::make_shader_material(mat, false);
	if (smat.is_null()) {
		return smat;
	}

	List<PropertyInfo> params;
	RS::get_singleton()->get_shader_parameter_list(mat->get_shader_rid(), &params);

	for (const PropertyInfo &E : params) {
		// Texture parameter has to be treated specially since ORMMaterial3D saved it
		// as RID but ShaderMaterial needs Texture itself
		Ref<Texture2D> texture = mat->get_texture_by_name(E.name);
		if (texture.is_valid()) {
			smat->set_shader_parameter(E.name, texture);
		} else {
			Variant value = RS::get_singleton()->material_get_param(mat->get_rid(), E.name);
			smat->set_shader_parameter(E.name, value);
		}
	}
	return smat;
}

String ProceduralSkyMaterialConversionPlugin::converts_to() const {
	return "ShaderMaterial";
}

bool ProceduralSkyMaterialConversionPlugin::handles(const Ref<Resource> &p_resource) const {
	Ref<ProceduralSkyMaterial> mat = p_resource;
	return mat.is_valid();
}

Ref<Resource> ProceduralSkyMaterialConversionPlugin::convert(const Ref<Resource> &p_resource) const {
	return MaterialEditor::make_shader_material(p_resource);
}

String PanoramaSkyMaterialConversionPlugin::converts_to() const {
	return "ShaderMaterial";
}

bool PanoramaSkyMaterialConversionPlugin::handles(const Ref<Resource> &p_resource) const {
	Ref<PanoramaSkyMaterial> mat = p_resource;
	return mat.is_valid();
}

Ref<Resource> PanoramaSkyMaterialConversionPlugin::convert(const Ref<Resource> &p_resource) const {
	return MaterialEditor::make_shader_material(p_resource);
}

String PhysicalSkyMaterialConversionPlugin::converts_to() const {
	return "ShaderMaterial";
}

bool PhysicalSkyMaterialConversionPlugin::handles(const Ref<Resource> &p_resource) const {
	Ref<PhysicalSkyMaterial> mat = p_resource;
	return mat.is_valid();
}

Ref<Resource> PhysicalSkyMaterialConversionPlugin::convert(const Ref<Resource> &p_resource) const {
	return MaterialEditor::make_shader_material(p_resource);
}

String FogMaterialConversionPlugin::converts_to() const {
	return "ShaderMaterial";
}

bool FogMaterialConversionPlugin::handles(const Ref<Resource> &p_resource) const {
	Ref<FogMaterial> mat = p_resource;
	return mat.is_valid();
}

Ref<Resource> FogMaterialConversionPlugin::convert(const Ref<Resource> &p_resource) const {
	return MaterialEditor::make_shader_material(p_resource);
}
