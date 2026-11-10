/**************************************************************************/
/*  shader_compile.h                                                      */

#pragma once

#include "systems/rendering/rendering_device_commons.h"

Vector<uint8_t> compile_glslang_shader(RenderingDeviceCommons::ShaderStage p_stage, const String &p_source_code, RenderingDeviceCommons::ShaderLanguageVersion p_language_version, RenderingDeviceCommons::ShaderSpirvVersion p_spirv_version, String *r_error);
