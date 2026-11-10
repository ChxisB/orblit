/**************************************************************************/
/*  resource_importer_texture_settings.h                                  */

#pragma once

// ResourceImporterTextureSettings contains code used by
// multiple texture importers and the export dialog.
namespace ResourceImporterTextureSettings {
bool should_import_s3tc_bptc();
bool should_import_etc2_astc();
} //namespace ResourceImporterTextureSettings
