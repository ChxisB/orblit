/**************************************************************************/
/*  gradle_export_util.h                                                  */

#pragma once

#include "core/crypto/crypto_core.h"
#include "editor/export/editor_export_platform.h"
#include "systems/display/display_system_enums.h"

const String ORBLIT_PROJECT_NAME_XML_STRING = R"(<?xml version="1.0" encoding="utf-8"?>
<!--WARNING: THIS FILE WILL BE OVERWRITTEN AT BUILD TIME-->
<resources>
	<string name="orblit_project_name_string">%s</string>
</resources>
)";

// Application category.
// See https://developer.android.com/guide/topics/manifest/application-element#appCategory for standards
static const int APP_CATEGORY_ACCESSIBILITY = 0;
static const int APP_CATEGORY_AUDIO = 1;
static const int APP_CATEGORY_GAME = 2;
static const int APP_CATEGORY_IMAGE = 3;
static const int APP_CATEGORY_MAPS = 4;
static const int APP_CATEGORY_NEWS = 5;
static const int APP_CATEGORY_PRODUCTIVITY = 6;
static const int APP_CATEGORY_SOCIAL = 7;
static const int APP_CATEGORY_VIDEO = 8;
static const int APP_CATEGORY_UNDEFINED = 9;

// Supported XR modes.
// This should match the entries in 'platform/android/java/lib/src/main/java/org/orblitengine/orblit/xr/XRMode.java'
static const int XR_MODE_REGULAR = 0;
static const int XR_MODE_OPENXR = 1;

// Supported export format
static const int EXPORT_FORMAT_APK = 0;
static const int EXPORT_FORMAT_AAB = 1;
static const int EXPORT_FORMAT_AAR = 2;

struct CustomExportData {
	EditorExportPlatform::PackData pd;
	String assets_directory;
	String libs_directory;
	bool debug;
	Vector<String> libs;
};

struct MetadataInfo {
	String name;
	String value;
};

int _get_android_orientation_value(DisplaySystemEnums::ScreenOrientation screen_orientation);

String _get_android_orientation_label(DisplaySystemEnums::ScreenOrientation screen_orientation);

int _get_app_category_value(int category_index);

String _get_app_category_label(int category_index);

Error _store_temp_file(const Ref<EditorExportPreset> &p_preset, const String &p_simplified_path, const Vector<uint8_t> &p_data, Vector<uint8_t> &r_enc_data, EditorExportPlatform::SavedData &r_sd);

// Utility method used to create a directory.
Error create_directory(const String &p_dir);

// Writes p_data into a file at p_path, creating directories if necessary.
// Note: this will overwrite the file at p_path if it already exists.
Error store_file_at_path(const String &p_path, const Vector<uint8_t> &p_data);

// Writes string p_data into a file at p_path, creating directories if necessary.
// Note: this will overwrite the file at p_path if it already exists.
Error store_string_at_path(const String &p_path, const String &p_data);

// Implementation of EditorExportPlatform::SaveFileFunction.
// This method will only be called as an input to export_project_files.
// It is used by the export_project_files method to save all the asset files into the gradle project.
// It's functionality mirrors that of the method save_apk_file.
// This method will be called ONLY when gradle build is enabled.
Error rename_and_store_file_in_gradle_project(const Ref<EditorExportPreset> &p_preset, void *p_userdata, const EditorExportPlatform::SaveFileInfo &p_info, const Vector<uint8_t> &p_data);

// Creates strings.xml files inside the gradle project for different locales.
Error _create_project_name_strings_files(const Ref<EditorExportPreset> &p_preset, const String &p_project_name, const String &p_export_src_dir, const Dictionary &p_appnames);

String bool_to_string(bool v);

String _get_gles_tag();

String _get_screen_sizes_tag(const Ref<EditorExportPreset> &p_preset);

String _get_activity_tag(const Ref<EditorExportPlatform> &p_export_platform, const Ref<EditorExportPreset> &p_preset, bool p_debug);

String _get_application_tag(const Ref<EditorExportPlatform> &p_export_platform, const Ref<EditorExportPreset> &p_preset, int p_export_format, bool p_has_read_write_storage_permission, bool p_debug, const Vector<MetadataInfo> &p_metadata);
