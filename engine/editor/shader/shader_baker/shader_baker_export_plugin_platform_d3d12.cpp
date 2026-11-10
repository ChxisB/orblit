/**************************************************************************/
/*  shader_baker_export_plugin_platform_d3d12.cpp                         */

#include "shader_baker_export_plugin_platform_d3d12.h"

#include "backends/d3d12/rendering_shader_container_d3d12.h"

#include <windows.h>

RenderingShaderContainerFormat *ShaderBakerExportPluginPlatformD3D12::create_shader_container_format(const Ref<EditorExportPlatform> &p_platform, const Ref<EditorExportPreset> &p_preset) {
	if (lib_d3d12 == nullptr) {
		lib_d3d12 = LoadLibraryW(L"D3D12.dll");
		ERR_FAIL_NULL_V_MSG(lib_d3d12, nullptr, "Unable to load D3D12.dll.");
	}

	// Shader Model 6.2 is required to export shaders that have FP16 variants.
	RenderingShaderContainerFormatD3D12 *shader_container_format_d3d12 = memnew(RenderingShaderContainerFormatD3D12);
	shader_container_format_d3d12->set_lib_d3d12(lib_d3d12);
	return shader_container_format_d3d12;
}

bool ShaderBakerExportPluginPlatformD3D12::matches_driver(const String &p_driver) {
	return p_driver == "d3d12";
}

ShaderBakerExportPluginPlatformD3D12 ::~ShaderBakerExportPluginPlatformD3D12() {
	if (lib_d3d12 != nullptr) {
		FreeLibrary((HMODULE)(lib_d3d12));
	}
}
