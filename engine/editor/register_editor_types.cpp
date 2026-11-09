/**************************************************************************/
/*  register_editor_types.cpp                                             */

#include "register_editor_types.h"

#include "core/config/engine.h"
#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"
#include "core/object/class_registry.h"
#include "core/object/script_language.h"
#include "core/os/os.h"
#include "editor/animation/animation_tree_editor_plugin.h"
#include "editor/audio/audio_stream_randomizer_editor_plugin.h"
#include "editor/debugger/debug_adapter/debug_adapter_server.h"
#include "editor/debugger/editor_debugger_plugin.h"
#include "editor/docks/filesystem_dock.h"
#include "editor/editor_interface.h"
#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "editor/editor_undo_redo_manager.h"
#include "editor/export/editor_export_platform.h"
#include "editor/export/editor_export_platform_apple_embedded.h"
#include "editor/export/editor_export_platform_extension.h"
#include "editor/export/editor_export_platform_pc.h"
#include "editor/export/editor_export_plugin.h"
#include "editor/export/register_exporters.h"
#include "editor/file_system/editor_file_system.h"
#include "editor/file_system/editor_paths.h"
#include "editor/widgets/editor_file_dialog.h"
#include "editor/widgets/editor_spin_slider.h"
#include "editor/widgets/editor_toaster.h"
#include "editor/import/spatial/resource_importer_obj.h"
#include "editor/import/spatial/resource_importer_scene.h"
#include "editor/import/editor_import_plugin.h"
#include "editor/import/resource_importer_bitmask.h"
#include "editor/import/resource_importer_bmfont.h"
#include "editor/import/resource_importer_csv_translation.h"
#include "editor/import/resource_importer_dynamic_font.h"
#include "editor/import/resource_importer_image.h"
#include "editor/import/resource_importer_imagefont.h"
#include "editor/import/resource_importer_layered_texture.h"
#include "editor/import/resource_importer_shader_file.h"
#include "editor/import/resource_importer_streamed_texture.h"
#include "editor/import/resource_importer_svg.h"
#include "editor/import/resource_importer_texture.h"
#include "editor/import/resource_importer_texture_atlas.h"
#include "editor/import/resource_importer_wav.h"
#include "editor/inspector/editor_context_menu_plugin.h"
#include "editor/inspector/editor_resource_picker.h"
#include "editor/inspector/editor_resource_preview.h"
#include "editor/inspector/editor_resource_tooltip_plugins.h"
#include "editor/inspector/input_event_editor_plugin.h"
#include "editor/inspector/sub_viewport_preview_editor_plugin.h"
#include "editor/inspector/tool_button_editor_plugin.h"
#include "editor/scene/planar/camera_planar_editor_plugin.h"
#include "editor/scene/planar/light_occluder_planar_editor_plugin.h"
#include "editor/scene/planar/line_planar_editor_plugin.h"
#include "editor/scene/planar/particles_planar_editor_plugin.h"
#include "editor/scene/planar/path_planar_editor_plugin.h"
#include "editor/scene/planar/physics/cast_planar_editor_plugin.h"
#include "editor/scene/planar/physics/collision_polygon_planar_editor_plugin.h"
#include "editor/scene/planar/physics/collision_shape_planar_editor_plugin.h"
#include "editor/scene/planar/polygon_planar_editor_plugin.h"
#include "editor/scene/planar/scene_paint_planar_editor_plugin.h"
#include "editor/scene/planar/skeleton_planar_editor_plugin.h"
#include "editor/scene/planar/sprite_planar_editor_plugin.h"
#include "editor/scene/spatial/bone_map_editor_plugin.h"
#include "editor/scene/spatial/camera_spatial_editor_plugin.h"
#include "editor/scene/spatial/gpu_particles_collision_sdf_editor_plugin.h"
#include "editor/scene/spatial/lightmap_gi_editor_plugin.h"
#include "editor/scene/spatial/mesh_editor_plugin.h"
#include "editor/scene/spatial/mesh_instance_spatial_editor_plugin.h"
#include "editor/scene/spatial/mesh_library_editor_plugin.h"
#include "editor/scene/spatial/multimesh_editor_plugin.h"
#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"
#include "editor/scene/spatial/occluder_instance_spatial_editor_plugin.h"
#include "editor/scene/spatial/particles_spatial_editor_plugin.h"
#include "editor/scene/spatial/path_spatial_editor_plugin.h"
#include "editor/scene/spatial/physics/physical_bone_spatial_editor_plugin.h"
#include "editor/scene/spatial/polygon_spatial_editor_plugin.h"
#include "editor/scene/spatial/skeleton_spatial_editor_plugin.h"
#include "editor/scene/spatial/voxel_gi_editor_plugin.h"
#include "editor/scene/curve_editor_plugin.h"
#include "editor/scene/gradient_editor_plugin.h"
#include "editor/scene/ui/control_editor_plugin.h"
#include "editor/scene/ui/font_config_plugin.h"
#include "editor/scene/ui/margin_container_editor_plugin.h"
#include "editor/scene/ui/style_box_editor_plugin.h"
#include "editor/scene/ui/theme_editor_plugin.h"
#include "editor/scene/ui/virtual_joystick_editor_plugin.h"
#include "editor/scene/material_editor_plugin.h"
#include "editor/scene/prefab_editor_plugin.h"
#include "editor/scene/resource_preloader_editor_plugin.h"
#include "editor/scene/sprite_frames_editor_plugin.h"
#include "editor/scene/texture/bit_map_editor_plugin.h"
#include "editor/scene/texture/gradient_texture_planar_editor_plugin.h"
#include "editor/scene/texture/texture_spatial_editor_plugin.h"
#include "editor/scene/texture/texture_editor_plugin.h"
#include "editor/scene/texture/texture_layered_editor_plugin.h"
#include "editor/scene/texture/texture_region_editor_plugin.h"
#include "editor/script/editor_script.h"
#include "editor/script/editor_script_plugin.h"
#include "editor/script/script_editor_plugin.h"
#include "editor/script/syntax_highlighters.h"
#include "editor/settings/editor_command_palette.h"
#include "editor/settings/editor_feature_profile.h"
#include "editor/settings/editor_settings.h"
#include "editor/shader/shader_editor_plugin.h"
#include "editor/shader/shader_file_editor_plugin.h"
#include "editor/translations/editor_translation_parser.h"
#include "editor/version_control/editor_vcs_interface.h"
#include "systems/rendering/rendering_system.h"

void register_editor_types() {
	OS::get_singleton()->benchmark_begin_measure("Editor", "Register Types");

	ResourceLoader::set_timestamp_on_load(true);
	ResourceSaver::set_timestamp_on_save(true);

	EditorStringNames::create();

	ORBLIT_REGISTER_CLASS(EditorPaths);
	ORBLIT_REGISTER_ABSTRACT_CLASS(ScenePaint2DEditor);
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorPlugin);
	ORBLIT_REGISTER_CLASS(EditorTranslationParserPlugin);
	ORBLIT_REGISTER_CLASS(EditorImportPlugin);
	ORBLIT_REGISTER_CLASS(EditorScript);
	ORBLIT_REGISTER_CLASS(EditorDock);
	ORBLIT_REGISTER_CLASS(EditorSelection);
	ORBLIT_REGISTER_CLASS(EditorFileDialog);
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorSettings);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorToaster);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorResourcePreview);
	ORBLIT_REGISTER_CLASS(EditorResourcePreviewGenerator);
	ORBLIT_REGISTER_CLASS(EditorResourceTooltipPlugin);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorFileSystem);
	ORBLIT_REGISTER_CLASS(EditorFileSystemDirectory);
	ORBLIT_REGISTER_CLASS(EditorVCSInterface);
	ORBLIT_REGISTER_ABSTRACT_CLASS(ScriptEditor);
	ORBLIT_REGISTER_ABSTRACT_CLASS(ScriptEditorBase);
	ORBLIT_REGISTER_CLASS(EditorSyntaxHighlighter);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorInterface);
	ORBLIT_REGISTER_CLASS(EditorExportPlugin);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorExportPlatform);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorExportPlatformPC);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorExportPlatformAppleEmbedded);
	ORBLIT_REGISTER_CLASS(EditorExportPlatformExtension);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorExportPreset);

	register_exporter_types();

	ORBLIT_REGISTER_CLASS(EditorResourceConversionPlugin);
	ORBLIT_REGISTER_CLASS(EditorInspector);
	ORBLIT_REGISTER_CLASS(EditorInspectorPlugin);
	ORBLIT_REGISTER_CLASS(EditorProperty);
	ORBLIT_REGISTER_CLASS(ScriptCreateDialog);
	ORBLIT_REGISTER_CLASS(EditorFeatureProfile);
	ORBLIT_REGISTER_CLASS(EditorSpinSlider);
	ORBLIT_REGISTER_CLASS(EditorResourcePicker);
	ORBLIT_REGISTER_CLASS(EditorScriptPicker);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorUndoRedoManager);
	ORBLIT_REGISTER_CLASS(EditorContextMenuPlugin);

	ORBLIT_REGISTER_ABSTRACT_CLASS(FileSystemDock);
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorFileSystemImportFormatSupportQuery);

	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorCommandPalette);
	ORBLIT_REGISTER_CLASS(EditorDebuggerPlugin);
	ORBLIT_REGISTER_ABSTRACT_CLASS(EditorDebuggerSession);

	// 3D editor foundational classes.
	ORBLIT_REGISTER_CLASS(EditorEntity3DGizmo);
	ORBLIT_REGISTER_CLASS(EditorEntity3DGizmoPlugin);
	ORBLIT_REGISTER_CLASS(EditorSceneFormatImporter);
	ORBLIT_REGISTER_CLASS(EditorScenePostImportPlugin);
	ORBLIT_REGISTER_CLASS(EditorScenePostImport);

	// Required to document import options in the class reference.
	ORBLIT_REGISTER_CLASS(ResourceImporterBitMap);
	ORBLIT_REGISTER_CLASS(ResourceImporterBMFont);
	ORBLIT_REGISTER_CLASS(ResourceImporterCSVTranslation);
	ORBLIT_REGISTER_CLASS(ResourceImporterDynamicFont);
	ORBLIT_REGISTER_CLASS(ResourceImporterImage);
	ORBLIT_REGISTER_CLASS(ResourceImporterImageFont);
	ORBLIT_REGISTER_CLASS(ResourceImporterSVG);
	ORBLIT_REGISTER_CLASS(ResourceImporterLayeredTexture);
	ORBLIT_REGISTER_CLASS(ResourceImporterOBJ);
	ORBLIT_REGISTER_CLASS(ResourceImporterScene);
	ORBLIT_REGISTER_CLASS(ResourceImporterShaderFile);
	ORBLIT_REGISTER_CLASS(ResourceImporterTexture);
	ORBLIT_REGISTER_CLASS(ResourceImporterTextureAtlas);
	ORBLIT_REGISTER_CLASS(ResourceImporterWAV);
	ORBLIT_REGISTER_CLASS(ResourceImporterStreamedTexture);

	// This list is alphabetized, and plugins that depend on Entity2D or Entity3D are in their own section below.
	EditorPlugins::add_by_type<AnimationTreeEditorPlugin>();
	EditorPlugins::add_by_type<AudioStreamRandomizerEditorPlugin>();
	EditorPlugins::add_by_type<BitMapEditorPlugin>();
	EditorPlugins::add_by_type<ControlEditorPlugin>();
	EditorPlugins::add_by_type<CurveEditorPlugin>();
	if (!Engine::get_singleton()->is_recovery_mode_hint()) {
		EditorPlugins::add_by_type<DebugAdapterServer>();
	}
	EditorPlugins::add_by_type<EditorScriptPlugin>();
	EditorPlugins::add_by_type<FontEditorPlugin>();
	EditorPlugins::add_by_type<GradientEditorPlugin>();
	EditorPlugins::add_by_type<GradientTexture2DEditorPlugin>();
	EditorPlugins::add_by_type<InputEventEditorPlugin>();
	EditorPlugins::add_by_type<MarginContainerEditorPlugin>();
	EditorPlugins::add_by_type<MaterialEditorPlugin>();
	EditorPlugins::add_by_type<PrefabEditorPlugin>();
	EditorPlugins::add_by_type<ResourcePreloaderEditorPlugin>();
	EditorPlugins::add_by_type<ShaderEditorPlugin>();
	EditorPlugins::add_by_type<ShaderFileEditorPlugin>();
	EditorPlugins::add_by_type<SpriteFramesEditorPlugin>();
	EditorPlugins::add_by_type<StyleBoxEditorPlugin>();
	EditorPlugins::add_by_type<SubViewportPreviewEditorPlugin>();
	EditorPlugins::add_by_type<Texture3DEditorPlugin>();
	EditorPlugins::add_by_type<TextureEditorPlugin>();
	EditorPlugins::add_by_type<TextureLayeredEditorPlugin>();
	EditorPlugins::add_by_type<TextureRegionEditorPlugin>();
	EditorPlugins::add_by_type<ThemeEditorPlugin>();
	EditorPlugins::add_by_type<ToolButtonEditorPlugin>();
	EditorPlugins::add_by_type<VirtualJoystickEditorPlugin>();

	// Entity2D-based editor plugins.
	EditorPlugins::add_by_type<Camera2DEditorPlugin>();
	EditorPlugins::add_by_type<CPUParticles2DEditorPlugin>();
	EditorPlugins::add_by_type<GPUParticles2DEditorPlugin>();
	EditorPlugins::add_by_type<LightOccluder2DEditorPlugin>();
	EditorPlugins::add_by_type<Line2DEditorPlugin>();
	EditorPlugins::add_by_type<Path2DEditorPlugin>();
	EditorPlugins::add_by_type<Polygon2DEditorPlugin>();
	EditorPlugins::add_by_type<Skeleton2DEditorPlugin>();
	EditorPlugins::add_by_type<Sprite2DEditorPlugin>();
	EditorPlugins::add_by_type<ScenePaint2DEditorPlugin>();
	// 2D physics editor plugins.
	EditorPlugins::add_by_type<Cast2DEditorPlugin>();
	EditorPlugins::add_by_type<CollisionPolygon2DEditorPlugin>();
	EditorPlugins::add_by_type<CollisionShape2DEditorPlugin>();

	// 3D editor plugins.
	EditorPlugins::add_by_type<BoneMapEditorPlugin>();
	EditorPlugins::add_by_type<Camera3DEditorPlugin>();
	EditorPlugins::add_by_type<CPUParticles3DEditorPlugin>();
	EditorPlugins::add_by_type<GPUParticles3DEditorPlugin>();
	EditorPlugins::add_by_type<GPUParticlesCollisionSDF3DEditorPlugin>();
	EditorPlugins::add_by_type<LightmapGIEditorPlugin>();
	EditorPlugins::add_by_type<MeshEditorPlugin>();
	EditorPlugins::add_by_type<MeshInstance3DEditorPlugin>();
	EditorPlugins::add_by_type<MeshLibraryEditorPlugin>();
	EditorPlugins::add_by_type<OccluderInstance3DEditorPlugin>();
	EditorPlugins::add_by_type<MultiMeshEditorPlugin>();
	EditorPlugins::add_by_type<Path3DEditorPlugin>();
	EditorPlugins::add_by_type<Polygon3DEditorPlugin>();
	EditorPlugins::add_by_type<Skeleton3DEditorPlugin>();
	EditorPlugins::add_by_type<VoxelGIEditorPlugin>();
	// 3D physics editor plugins.
	EditorPlugins::add_by_type<PhysicalBone3DEditorPlugin>();

	// For correct doc generation.
	GLOBAL_DEF(PropertyInfo(Variant::STRING, "editor/run/main_run_args", PROPERTY_HINT_NONE, "monospace"), "");

	GLOBAL_DEF(PropertyInfo(Variant::STRING, "editor/script/templates_search_path", PROPERTY_HINT_DIR), "res://script_templates");

	GLOBAL_DEF("editor/naming/default_signal_callback_name", "_on_{node_name}_{signal_name}");
	GLOBAL_DEF("editor/naming/default_signal_callback_to_self_name", "_on_{signal_name}");
	GLOBAL_DEF(PropertyInfo(Variant::INT, "editor/naming/scene_name_casing", PROPERTY_HINT_ENUM, "Auto,PascalCase,snake_case,kebab-case,camelCase"), EditorNode::SCENE_NAME_CASING_SNAKE_CASE);
	GLOBAL_DEF(PropertyInfo(Variant::INT, "editor/naming/script_name_casing", PROPERTY_HINT_ENUM, "Auto,PascalCase,snake_case,kebab-case,camelCase"), ScriptLanguage::SCRIPT_NAME_CASING_AUTO);

	GLOBAL_DEF("editor/import/reimport_missing_imported_files", true);
	GLOBAL_DEF("editor/import/use_multiple_threads", true);

	GLOBAL_DEF(PropertyInfo(Variant::INT, "editor/import/atlas_max_width", PROPERTY_HINT_RANGE, "128,8192,1,or_greater"), 2048);

	GLOBAL_DEF("editor/export/convert_text_resources_to_binary", true);

	GLOBAL_DEF("editor/version_control/plugin_name", "");
	GLOBAL_DEF("editor/version_control/autoload_on_startup", false);

	EditorInterface::create();
	Engine::Singleton ei_singleton = Engine::Singleton("EditorInterface", EditorInterface::get_singleton());
	ei_singleton.editor_only = true;
	Engine::get_singleton()->add_singleton(ei_singleton);

	if (RenderingSystem::get_singleton()) {
		// RenderingSystem needs to exist for this to succeed.
		Texture3DEditor::init_shaders();
		TextureLayeredEditor::init_shaders();
		TexturePreview::init_shaders();
	}

	// Required as NativeExtensions can register docs at init time way before this
	// class is actually instantiated.
	EditorHelp::init_gdext_pointers();

	OS::get_singleton()->benchmark_end_measure("Editor", "Register Types");
}

void unregister_editor_types() {
	OS::get_singleton()->benchmark_begin_measure("Editor", "Unregister Types");

	Texture3DEditor::finish_shaders();
	TextureLayeredEditor::finish_shaders();
	TexturePreview::finish_shaders();

	EditorInspector::set_property_clipboard(EditorInspector::PropertyClipboard::Type::EMPTY, Variant());
	EditorNode::cleanup();
	EditorInterface::free();

	if (EditorPaths::get_singleton()) {
		EditorPaths::free();
	}
	EditorStringNames::free();

	OS::get_singleton()->benchmark_end_measure("Editor", "Unregister Types");
}
