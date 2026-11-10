/**************************************************************************/
/*  shader_baker_export_plugin_platform_metal.cpp                         */

#include "shader_baker_export_plugin_platform_metal.h"

#include "backends/metal/rendering_shader_container_metal.h"

RenderingShaderContainerFormat *ShaderBakerExportPluginPlatformMetal::create_shader_container_format(const Ref<EditorExportPlatform> &p_platform, const Ref<EditorExportPreset> &p_preset) {
	const String &os_name = p_platform->get_os_name();
	const MetalDeviceProfile *profile;
	MinOsVersion min_os_version;

	if (os_name == U"macOS") {
		min_os_version = (String)p_preset->get("application/min_macos_version_arm64");
		// Orblit metal doesn't support x86_64 mac so no need to worry about that version
		profile = MetalDeviceProfile::get_profile(MetalDeviceProfile::Platform::macOS, MetalDeviceProfile::GPU::Apple7, min_os_version);
	} else if (os_name == U"iOS") {
		min_os_version = (String)p_preset->get("application/min_ios_version");
		profile = MetalDeviceProfile::get_profile(MetalDeviceProfile::Platform::iOS, MetalDeviceProfile::GPU::Apple7, min_os_version);
	} else if (os_name == U"visionOS") {
		min_os_version = (String)p_preset->get("application/min_visionos_version");
		profile = MetalDeviceProfile::get_profile(MetalDeviceProfile::Platform::visionOS, MetalDeviceProfile::GPU::Apple8, min_os_version);
	} else {
		ERR_FAIL_V_MSG(nullptr, vformat("Unsupported platform: %s", os_name));
	}
	ERR_FAIL_NULL_V(profile, nullptr);
	return memnew(RenderingShaderContainerFormatMetal(profile, true));
}

bool ShaderBakerExportPluginPlatformMetal::matches_driver(const String &p_driver) {
	return p_driver == "metal";
}
