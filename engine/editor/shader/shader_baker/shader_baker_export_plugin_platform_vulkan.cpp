/**************************************************************************/
/*  shader_baker_export_plugin_platform_vulkan.cpp                        */

#include "shader_baker_export_plugin_platform_vulkan.h"

#include "backends/vulkan/rendering_shader_container_vulkan.h"

RenderingShaderContainerFormat *ShaderBakerExportPluginPlatformVulkan::create_shader_container_format(const Ref<EditorExportPlatform> &p_platform, const Ref<EditorExportPreset> &p_preset) {
	return memnew(RenderingShaderContainerFormatVulkan);
}

bool ShaderBakerExportPluginPlatformVulkan::matches_driver(const String &p_driver) {
	return p_driver == "vulkan";
}
