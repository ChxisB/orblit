/**************************************************************************/
/*  register_scene_types.cpp                                              */

#include "register_scene_types.h"

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"
#include "core/object/class_registry.h"
#include "core/os/os.h"
#include "scene/animation/animation_blend_space_1d.h"
#include "scene/animation/animation_blend_space_planar.h"
#include "scene/animation/animation_blend_tree.h"
#include "scene/animation/animation_mixer.h"
#include "scene/animation/animation_node_extension.h"
#include "scene/animation/animation_node_state_machine.h"
#include "scene/animation/animation_player.h"
#include "scene/animation/animation_tree.h"
#include "scene/animation/tween.h"
#include "scene/audio/audio_stream_player.h"
#include "scene/debugger/scene_debugger.h"
#include "scene/resources/animation_library.h"
#include "scene/resources/atlas_texture.h"
#include "scene/resources/audio/audio_stream.h"
#include "scene/resources/audio/audio_stream_microphone.h"
#include "scene/resources/audio/audio_stream_polyphonic.h"
#include "scene/resources/audio/audio_stream_randomizer.h"
#include "scene/resources/audio/audio_stream_wav.h"
#include "scene/resources/bit_map.h"
#include "scene/resources/blit_material.h"
#include "scene/resources/bone_map.h"
#include "scene/resources/camera_attributes.h"
#include "scene/resources/camera_texture.h"
#include "scene/resources/canvas_item_material.h"
#include "scene/resources/color_palette.h"
#include "scene/resources/compositor.h"
#include "scene/resources/compressed_texture.h"
#include "scene/resources/compressed_texture_resource_format.h"
#include "scene/resources/curve_texture.h"
#include "scene/resources/drawable_texture_planar.h"
#include "scene/resources/environment.h"
#include "scene/resources/external_texture.h"
#include "scene/resources/font.h"
#include "scene/resources/gradient.h"
#include "scene/resources/gradient_texture.h"
#include "scene/resources/image_texture.h"
#include "scene/resources/immediate_mesh.h"
#include "scene/resources/label_settings.h"
#include "scene/resources/material.h"
#include "scene/resources/mesh_data_tool.h"
#include "scene/resources/mesh_texture.h"
#include "scene/resources/multimesh.h"
#include "scene/stage/canvas_item.h"
#include "scene/stage/canvas_layer.h"
#include "scene/stage/http_request.h"
#include "scene/stage/instance_placeholder.h"
#include "scene/stage/missing_node.h"
#include "scene/stage/multiplayer_api.h"
#include "scene/stage/resource_preloader.h"
#include "scene/stage/shader_globals_override.h"
#include "scene/stage/stage.h"
#include "scene/stage/status_indicator.h"
#include "scene/stage/timer.h"
#include "scene/stage/viewport.h"
#include "scene/stage/window.h"
#include "scene/ui/aspect_ratio_container.h"
#include "scene/ui/box_container.h"
#include "scene/ui/button.h"
#include "scene/ui/center_container.h"
#include "scene/ui/check_box.h"
#include "scene/ui/check_button.h"
#include "scene/ui/code_edit.h"
#include "scene/ui/color_picker.h"
#include "scene/ui/color_picker_shape.h"
#include "scene/ui/color_rect.h"
#include "scene/ui/control.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/file_dialog.h"
#include "scene/ui/flow_container.h"
#include "scene/ui/foldable_container.h"
#include "scene/ui/graph_edit.h"
#include "scene/ui/graph_frame.h"
#include "scene/ui/graph_node.h"
#include "scene/ui/grid_container.h"
#include "scene/ui/item_list.h"
#include "scene/ui/label.h"
#include "scene/ui/line_edit.h"
#include "scene/ui/link_button.h"
#include "scene/ui/margin_container.h"
#include "scene/ui/menu_bar.h"
#include "scene/ui/menu_button.h"
#include "scene/ui/nine_patch_rect.h"
#include "scene/ui/option_button.h"
#include "scene/ui/panel.h"
#include "scene/ui/panel_container.h"
#include "scene/ui/popup_menu.h"
#include "scene/ui/progress_bar.h"
#include "scene/ui/reference_rect.h"
#include "scene/ui/rich_text_effect.h"
#include "scene/ui/rich_text_label.h"
#include "scene/ui/scroll_bar.h"
#include "scene/ui/scroll_container.h"
#include "scene/ui/separator.h"
#include "scene/ui/slider.h"
#include "scene/ui/spin_box.h"
#include "scene/ui/split_container.h"
#include "scene/ui/subviewport_container.h"
#include "scene/ui/tab_bar.h"
#include "scene/ui/tab_container.h"
#include "scene/ui/text_edit.h"
#include "scene/ui/texture_button.h"
#include "scene/ui/texture_progress_bar.h"
#include "scene/ui/texture_rect.h"
#include "scene/ui/tree.h"
#include "scene/ui/video_stream_player.h"
#include "scene/ui/virtual_joystick.h"
#if !defined(NAVIGATION_PLANAR_DISABLED) || !defined(NAVIGATION_SPATIAL_DISABLED)
#include "scene/resources/navigation_mesh.h"
#endif // !defined(NAVIGATION_PLANAR_DISABLED) || !defined(NAVIGATION_SPATIAL_DISABLED)
#include "scene/resources/audio/audio_stream_generator.h"
#include "scene/resources/dpi_texture.h"
#include "scene/resources/particle_process_material.h"
#include "scene/resources/placeholder_textures.h"
#include "scene/resources/portable_compressed_texture.h"
#include "scene/resources/prefab.h"
#include "scene/resources/resource_format_text.h"
#include "scene/resources/shader_include.h"
#include "scene/resources/shader_include_resource_format.h"
#include "scene/resources/shader_resource_format.h"
#include "scene/resources/skeleton_profile.h"
#include "scene/resources/sky.h"
#include "scene/resources/streamed_texture.h"
#include "scene/resources/style_box.h"
#include "scene/resources/style_box_flat.h"
#include "scene/resources/style_box_line.h"
#include "scene/resources/style_box_texture.h"
#include "scene/resources/surface_tool.h"
#include "scene/resources/syntax_highlighter.h"
#include "scene/resources/text_line.h"
#include "scene/resources/text_paragraph.h"
#include "scene/resources/texture.h"
#include "scene/resources/texture_rd.h"
#include "scene/resources/theme.h"
#include "scene/resources/video_stream.h"
#include "scene/resources/world_planar.h"
#include "scene/theme/theme_db.h"
#include "systems/display/display_system.h"
#include "systems/rendering/rendering_system.h"

#ifndef PLANAR_DISABLED
#include "scene/planar/animated_sprite_planar.h"
#include "scene/planar/audio_listener_planar.h"
#include "scene/planar/audio_stream_player_planar.h"
#include "scene/planar/back_buffer_copy.h"
#include "scene/planar/camera_planar.h"
#include "scene/planar/canvas_group.h"
#include "scene/planar/canvas_modulate.h"
#include "scene/planar/cpu_particles_planar.h"
#include "scene/planar/gpu_particles_planar.h"
#include "scene/planar/light_occluder_planar.h"
#include "scene/planar/light_planar.h"
#include "scene/planar/line_planar.h"
#include "scene/planar/marker_planar.h"
#include "scene/planar/mesh_instance_planar.h"
#include "scene/planar/multimesh_instance_planar.h"
#include "scene/planar/parallax_planar.h"
#include "scene/planar/path_planar.h"
#include "scene/planar/polygon_planar.h"
#include "scene/planar/remote_transform_planar.h"
#include "scene/planar/skeleton_planar.h"
#include "scene/planar/sprite_planar.h"
#include "scene/planar/visible_on_screen_notifier_planar.h"
#include "scene/resources/planar/polygon_path_finder.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar_ccdik.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar_fabrik.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar_lookat.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar_stackholder.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar_twoboneik.h"
#include "scene/resources/planar/skeleton/skeleton_modification_stack_planar.h"
#endif // PLANAR_DISABLED

#ifndef NAVIGATION_PLANAR_DISABLED
#include "scene/planar/navigation/navigation_agent_planar.h"
#include "scene/planar/navigation/navigation_link_planar.h"
#include "scene/planar/navigation/navigation_obstacle_planar.h"
#include "scene/planar/navigation/navigation_region_planar.h"
#include "scene/resources/planar/navigation_mesh_source_geometry_data_planar.h"
#include "scene/resources/planar/navigation_polygon.h"
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef SPATIAL_DISABLED
#include "scene/animation/root_motion_view.h"
#include "scene/resources/spatial/fog_material.h"
#include "scene/resources/spatial/importer_mesh.h"
#include "scene/resources/spatial/joint_limitation_cone_spatial.h"
#include "scene/resources/spatial/joint_limitation_spatial.h"
#include "scene/resources/spatial/mesh_library.h"
#include "scene/resources/spatial/navigation_mesh_source_geometry_data_spatial.h"
#include "scene/resources/spatial/primitive_meshes.h"
#include "scene/resources/spatial/sky_material.h"
#include "scene/resources/spatial/world_spatial.h"
#include "scene/spatial/aim_modifier_spatial.h"
#include "scene/spatial/audio_listener_spatial.h"
#include "scene/spatial/audio_stream_player_spatial.h"
#include "scene/spatial/bone_attachment_spatial.h"
#include "scene/spatial/bone_constraint_spatial.h"
#include "scene/spatial/bone_space_adjuster_spatial.h"
#include "scene/spatial/bone_twist_disperser_spatial.h"
#include "scene/spatial/camera_spatial.h"
#include "scene/spatial/ccd_ik_spatial.h"
#include "scene/spatial/chain_ik_spatial.h"
#include "scene/spatial/convert_transform_modifier_spatial.h"
#include "scene/spatial/copy_transform_modifier_spatial.h"
#include "scene/spatial/cpu_particles_spatial.h"
#include "scene/spatial/decal.h"
#include "scene/spatial/entity_spatial.h"
#include "scene/spatial/fabr_ik_spatial.h"
#include "scene/spatial/fog_volume.h"
#include "scene/spatial/gpu_particles_collision_spatial.h"
#include "scene/spatial/gpu_particles_spatial.h"
#include "scene/spatial/ik_modifier_spatial.h"
#include "scene/spatial/importer_mesh_instance_spatial.h"
#include "scene/spatial/iterate_ik_spatial.h"
#include "scene/spatial/jacobian_ik_spatial.h"
#include "scene/spatial/label_spatial.h"
#include "scene/spatial/light_spatial.h"
#include "scene/spatial/lightmap_gi.h"
#include "scene/spatial/lightmap_probe.h"
#include "scene/spatial/limit_angular_velocity_modifier_spatial.h"
#include "scene/spatial/line_base_spatial.h"
#include "scene/spatial/look_at_modifier_spatial.h"
#include "scene/spatial/marker_spatial.h"
#include "scene/spatial/mesh_instance_spatial.h"
#include "scene/spatial/modifier_bone_target_spatial.h"
#include "scene/spatial/multimesh_instance_spatial.h"
#include "scene/spatial/occluder_instance_spatial.h"
#include "scene/spatial/path_spatial.h"
#include "scene/spatial/reflection_probe.h"
#include "scene/spatial/remote_transform_spatial.h"
#include "scene/spatial/retarget_modifier_spatial.h"
#include "scene/spatial/skeleton_modifier_spatial.h"
#include "scene/spatial/skeleton_spatial.h"
#include "scene/spatial/spline_ik_spatial.h"
#include "scene/spatial/spring_bone_collision_capsule_spatial.h"
#include "scene/spatial/spring_bone_collision_plane_spatial.h"
#include "scene/spatial/spring_bone_collision_spatial.h"
#include "scene/spatial/spring_bone_collision_sphere_spatial.h"
#include "scene/spatial/spring_bone_simulator_spatial.h"
#include "scene/spatial/sprite_spatial.h"
#include "scene/spatial/trail_spatial.h"
#include "scene/spatial/two_bone_ik_spatial.h"
#include "scene/spatial/visible_on_screen_notifier_spatial.h"
#include "scene/spatial/voxel_gi.h"
#include "scene/spatial/world_environment.h"
#ifndef NAVIGATION_SPATIAL_DISABLED
#include "scene/resources/spatial/navigation_mesh_source_geometry_data_spatial.h"
#include "scene/spatial/navigation/navigation_agent_spatial.h"
#include "scene/spatial/navigation/navigation_link_spatial.h"
#include "scene/spatial/navigation/navigation_obstacle_spatial.h"
#include "scene/spatial/navigation/navigation_region_spatial.h"
#endif // NAVIGATION_SPATIAL_DISABLED
#ifndef XR_DISABLED
#include "scene/spatial/xr/xr_body_modifier_spatial.h"
#include "scene/spatial/xr/xr_face_modifier_spatial.h"
#include "scene/spatial/xr/xr_hand_modifier_spatial.h"
#include "scene/spatial/xr/xr_nodes.h"
#endif // XR_DISABLED
#endif // SPATIAL_DISABLED

#if !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)
#include "scene/resources/physics_material.h"
#endif // !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)

#ifndef PHYSICS_PLANAR_DISABLED
#include "scene/planar/physics/animatable_body_planar.h"
#include "scene/planar/physics/area_planar.h"
#include "scene/planar/physics/character_body_planar.h"
#include "scene/planar/physics/collision_polygon_planar.h"
#include "scene/planar/physics/collision_shape_planar.h"
#include "scene/planar/physics/joints/damped_spring_joint_planar.h"
#include "scene/planar/physics/joints/groove_joint_planar.h"
#include "scene/planar/physics/joints/joint_planar.h"
#include "scene/planar/physics/joints/pin_joint_planar.h"
#include "scene/planar/physics/kinematic_collision_planar.h"
#include "scene/planar/physics/physical_bone_planar.h"
#include "scene/planar/physics/physics_body_planar.h"
#include "scene/planar/physics/ray_cast_planar.h"
#include "scene/planar/physics/rigid_body_planar.h"
#include "scene/planar/physics/shape_cast_planar.h"
#include "scene/planar/physics/static_body_planar.h"
#include "scene/planar/physics/touch_screen_button.h"
#include "scene/resources/planar/capsule_shape_planar.h"
#include "scene/resources/planar/circle_shape_planar.h"
#include "scene/resources/planar/concave_polygon_shape_planar.h"
#include "scene/resources/planar/convex_polygon_shape_planar.h"
#include "scene/resources/planar/rectangle_shape_planar.h"
#include "scene/resources/planar/segment_shape_planar.h"
#include "scene/resources/planar/separation_ray_shape_planar.h"
#include "scene/resources/planar/shape_planar.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar_jiggle.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar_physicalbones.h"
#include "scene/resources/planar/world_boundary_shape_planar.h"
#endif // PHYSICS_PLANAR_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
#include "scene/resources/spatial/box_shape_spatial.h"
#include "scene/resources/spatial/capsule_shape_spatial.h"
#include "scene/resources/spatial/concave_polygon_shape_spatial.h"
#include "scene/resources/spatial/convex_polygon_shape_spatial.h"
#include "scene/resources/spatial/cylinder_shape_spatial.h"
#include "scene/resources/spatial/height_map_shape_spatial.h"
#include "scene/resources/spatial/importer_mesh.h"
#include "scene/resources/spatial/mesh_library.h"
#include "scene/resources/spatial/navigation_mesh_source_geometry_data_spatial.h"
#include "scene/resources/spatial/primitive_meshes.h"
#include "scene/resources/spatial/separation_ray_shape_spatial.h"
#include "scene/resources/spatial/sphere_shape_spatial.h"
#include "scene/resources/spatial/world_boundary_shape_spatial.h"
#include "scene/spatial/physics/animatable_body_spatial.h"
#include "scene/spatial/physics/area_spatial.h"
#include "scene/spatial/physics/character_body_spatial.h"
#include "scene/spatial/physics/collision_polygon_spatial.h"
#include "scene/spatial/physics/collision_shape_spatial.h"
#include "scene/spatial/physics/joints/cone_twist_joint_spatial.h"
#include "scene/spatial/physics/joints/generic_6dof_joint_spatial.h"
#include "scene/spatial/physics/joints/hinge_joint_spatial.h"
#include "scene/spatial/physics/joints/joint_spatial.h"
#include "scene/spatial/physics/joints/pin_joint_spatial.h"
#include "scene/spatial/physics/joints/slider_joint_spatial.h"
#include "scene/spatial/physics/kinematic_collision_spatial.h"
#include "scene/spatial/physics/physical_bone_simulator_spatial.h"
#include "scene/spatial/physics/physical_bone_spatial.h"
#include "scene/spatial/physics/physics_body_spatial.h"
#include "scene/spatial/physics/ray_cast_spatial.h"
#include "scene/spatial/physics/rigid_body_spatial.h"
#include "scene/spatial/physics/shape_cast_spatial.h"
#include "scene/spatial/physics/soft_body_spatial.h"
#include "scene/spatial/physics/spring_arm_spatial.h"
#include "scene/spatial/physics/static_body_spatial.h"
#include "scene/spatial/physics/vehicle_body_spatial.h"
#endif // PHYSICS_SPATIAL_DISABLED

static Ref<ResourceFormatSaverText> resource_saver_text;
static Ref<ResourceFormatLoaderText> resource_loader_text;

static Ref<ResourceFormatLoaderCompressedTexture2D> resource_loader_compressed_texture;
static Ref<ResourceFormatLoaderStreamedTexture2D> resource_loader_streamed_texture;
static Ref<ResourceFormatLoaderCompressedTextureLayered> resource_loader_texture_layered;
static Ref<ResourceFormatLoaderCompressedTexture3D> resource_loader_texture_3d;

static Ref<ResourceFormatSaverShader> resource_saver_shader;
static Ref<ResourceFormatLoaderShader> resource_loader_shader;

static Ref<ResourceFormatSaverShaderInclude> resource_saver_shader_include;
static Ref<ResourceFormatLoaderShaderInclude> resource_loader_shader_include;

void register_scene_types() {
	OS::get_singleton()->benchmark_begin_measure("Scene", "Register Types");

	SceneStringNames::create();

	OS::get_singleton()->yield(); // may take time to init

	Entity::init_node_hrcr();

	if constexpr (ORBLIT_IS_CLASS_ENABLED(CompressedTexture2D)) {
		resource_loader_compressed_texture.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_compressed_texture);
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(StreamedTexture2D)) {
		resource_loader_streamed_texture.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_streamed_texture);
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(TextureLayered)) {
		resource_loader_texture_layered.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_texture_layered);
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Texture3D)) {
		resource_loader_texture_3d.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_texture_3d);
	}

	resource_saver_text.instantiate();
	ResourceSaver::add_resource_format_saver(resource_saver_text, true);

	resource_loader_text.instantiate();
	ResourceLoader::add_resource_format_loader(resource_loader_text, true);

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Shader)) {
		resource_saver_shader.instantiate();
		ResourceSaver::add_resource_format_saver(resource_saver_shader, true);

		resource_loader_shader.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_shader, true);
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(ShaderInclude)) {
		resource_saver_shader_include.instantiate();
		ResourceSaver::add_resource_format_saver(resource_saver_shader_include, true);

		resource_loader_shader_include.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_shader_include, true);
	}

	OS::get_singleton()->yield(); // may take time to init

	ORBLIT_REGISTER_CLASS(Entity);
	ORBLIT_REGISTER_CLASS(MissingNode);
	ORBLIT_REGISTER_ABSTRACT_CLASS(InstancePlaceholder);

	ORBLIT_REGISTER_ABSTRACT_CLASS(CanvasItem);

	ORBLIT_REGISTER_VIRTUAL_CLASS(Texture);
	ORBLIT_REGISTER_VIRTUAL_CLASS(Texture2D);

	ORBLIT_REGISTER_VIRTUAL_CLASS(Material);
	ORBLIT_REGISTER_CLASS(PlaceholderMaterial);

	ORBLIT_REGISTER_ABSTRACT_CLASS(Viewport);
	ORBLIT_REGISTER_CLASS(SubViewport);
	ORBLIT_REGISTER_CLASS(ViewportTexture);

	ORBLIT_REGISTER_VIRTUAL_CLASS(CompositorEffect);

	ORBLIT_REGISTER_ABSTRACT_CLASS(MultiplayerPeer);
	ORBLIT_REGISTER_CLASS(MultiplayerPeerExtension);
	ORBLIT_REGISTER_ABSTRACT_CLASS(MultiplayerAPI);
	ORBLIT_REGISTER_CLASS(MultiplayerAPIExtension);

	ORBLIT_REGISTER_CLASS(HTTPRequest);
	ORBLIT_REGISTER_CLASS(Timer);
	ORBLIT_REGISTER_CLASS(CanvasLayer);
	ORBLIT_REGISTER_CLASS(ResourcePreloader);
	ORBLIT_REGISTER_CLASS(Window);

	ORBLIT_REGISTER_CLASS(StatusIndicator);

	/* REGISTER GUI */

	OS::get_singleton()->yield(); // may take time to init

	ORBLIT_REGISTER_CLASS(Control);
	ORBLIT_REGISTER_VIRTUAL_CLASS(BaseButton);
	ORBLIT_REGISTER_CLASS(Button);
	ORBLIT_REGISTER_CLASS(Label);
	ORBLIT_REGISTER_VIRTUAL_CLASS(Range);
	ORBLIT_REGISTER_ABSTRACT_CLASS(ScrollBar);
	ORBLIT_REGISTER_CLASS(HScrollBar);
	ORBLIT_REGISTER_CLASS(VScrollBar);
	ORBLIT_REGISTER_CLASS(ProgressBar);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Slider);
	ORBLIT_REGISTER_CLASS(HSlider);
	ORBLIT_REGISTER_CLASS(VSlider);
	ORBLIT_REGISTER_CLASS(Popup);
	ORBLIT_REGISTER_CLASS(PopupPanel);
	ORBLIT_REGISTER_CLASS(CheckBox);
	ORBLIT_REGISTER_CLASS(CheckButton);
	ORBLIT_REGISTER_CLASS(LinkButton);
	ORBLIT_REGISTER_CLASS(Panel);
	ORBLIT_REGISTER_CLASS(ButtonGroup);

	OS::get_singleton()->yield(); // may take time to init

	ORBLIT_REGISTER_CLASS(Container);
	ORBLIT_REGISTER_CLASS(TextureRect);
	ORBLIT_REGISTER_CLASS(ColorRect);
	ORBLIT_REGISTER_CLASS(NinePatchRect);
	ORBLIT_REGISTER_CLASS(ReferenceRect);
	ORBLIT_REGISTER_CLASS(AspectRatioContainer);
	ORBLIT_REGISTER_CLASS(TabContainer);
	ORBLIT_REGISTER_CLASS(TabBar);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Separator);
	ORBLIT_REGISTER_CLASS(HSeparator);
	ORBLIT_REGISTER_CLASS(VSeparator);
	ORBLIT_REGISTER_CLASS(TextureButton);
	ORBLIT_REGISTER_CLASS(BoxContainer);
	ORBLIT_REGISTER_CLASS(HBoxContainer);
	ORBLIT_REGISTER_CLASS(VBoxContainer);
	ORBLIT_REGISTER_CLASS(GridContainer);
	ORBLIT_REGISTER_CLASS(CenterContainer);
	ORBLIT_REGISTER_CLASS(ScrollContainer);
	ORBLIT_REGISTER_CLASS(PanelContainer);
	ORBLIT_REGISTER_CLASS(FlowContainer);
	ORBLIT_REGISTER_CLASS(HFlowContainer);
	ORBLIT_REGISTER_CLASS(VFlowContainer);
	ORBLIT_REGISTER_CLASS(MarginContainer);

	OS::get_singleton()->yield(); // may take time to init

	ORBLIT_REGISTER_CLASS(TextureProgressBar);
	ORBLIT_REGISTER_CLASS(ItemList);

	ORBLIT_REGISTER_CLASS(LineEdit);
	ORBLIT_REGISTER_CLASS(VideoStreamPlayer);
	ORBLIT_REGISTER_VIRTUAL_CLASS(VideoStreamPlayback);
	ORBLIT_REGISTER_VIRTUAL_CLASS(VideoStream);

#ifndef ADVANCED_GUI_DISABLED
	ORBLIT_REGISTER_CLASS(AcceptDialog);
	ORBLIT_REGISTER_CLASS(ConfirmationDialog);

	ORBLIT_REGISTER_CLASS(FileDialog);

	ORBLIT_REGISTER_CLASS(PopupMenu);
	ORBLIT_REGISTER_CLASS(Tree);

	ORBLIT_REGISTER_CLASS(TextEdit);
	ORBLIT_REGISTER_CLASS(CodeEdit);
	ORBLIT_REGISTER_CLASS(SyntaxHighlighter);
	ORBLIT_REGISTER_CLASS(CodeHighlighter);

	ORBLIT_REGISTER_ABSTRACT_CLASS(TreeItem);
	ORBLIT_REGISTER_CLASS(MenuBar);
	ORBLIT_REGISTER_CLASS(MenuButton);
	ORBLIT_REGISTER_CLASS(OptionButton);
	ORBLIT_REGISTER_CLASS(SpinBox);
	ORBLIT_REGISTER_CLASS(ColorPicker);
	ORBLIT_REGISTER_CLASS(ColorPickerButton);
	ORBLIT_REGISTER_CLASS(RichTextLabel);
	ORBLIT_REGISTER_CLASS(RichTextEffect);
	ORBLIT_REGISTER_CLASS(CharFXTransform);

	ORBLIT_REGISTER_CLASS(SubViewportContainer);
	ORBLIT_REGISTER_CLASS(SplitContainer);
	ORBLIT_REGISTER_CLASS(HSplitContainer);
	ORBLIT_REGISTER_CLASS(VSplitContainer);

	ORBLIT_REGISTER_CLASS(GraphElement);
	ORBLIT_REGISTER_CLASS(GraphNode);
	ORBLIT_REGISTER_CLASS(GraphFrame);
	ORBLIT_REGISTER_CLASS(GraphEdit);

	ORBLIT_REGISTER_CLASS(FoldableGroup);
	ORBLIT_REGISTER_CLASS(FoldableContainer);

	ORBLIT_REGISTER_CLASS(VirtualJoystick);

	OS::get_singleton()->yield(); // may take time to init

	int swap_cancel_ok = GLOBAL_DEF(PropertyInfo(Variant::INT, "gui/common/swap_cancel_ok", PROPERTY_HINT_ENUM, "Auto,Cancel First,OK First"), 0);
	if (DisplaySystem::get_singleton() && swap_cancel_ok == 0) {
		swap_cancel_ok = DisplaySystem::get_singleton()->get_swap_cancel_ok() ? 2 : 1;
	}
	AcceptDialog::set_swap_cancel_ok(swap_cancel_ok == 2);
#endif

	int root_dir = GLOBAL_GET("internationalization/rendering/root_node_layout_direction");
	Control::set_root_layout_direction(root_dir);
	Window::set_root_layout_direction(root_dir);

	/* REGISTER ANIMATION */
	ORBLIT_REGISTER_CLASS(Tween);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Tweener);
	ORBLIT_REGISTER_CLASS(PropertyTweener);
	ORBLIT_REGISTER_CLASS(IntervalTweener);
	ORBLIT_REGISTER_CLASS(CallbackTweener);
	ORBLIT_REGISTER_CLASS(MethodTweener);
	ORBLIT_REGISTER_CLASS(SubtweenTweener);
	ORBLIT_REGISTER_CLASS(AwaitTweener);

	ORBLIT_REGISTER_ABSTRACT_CLASS(AnimationMixer);
	ORBLIT_REGISTER_CLASS(AnimationPlayer);
	ORBLIT_REGISTER_CLASS(AnimationTree);
	ORBLIT_REGISTER_CLASS(AnimationNode);
	ORBLIT_REGISTER_ABSTRACT_CLASS(AnimationNodeObserver);
	ORBLIT_REGISTER_CLASS(AnimationRootNode);
	ORBLIT_REGISTER_CLASS(AnimationNodeBlendTree);
	ORBLIT_REGISTER_CLASS(AnimationNodeBlendSpace1D);
	ORBLIT_REGISTER_CLASS(AnimationNodeBlendSpace2D);
	ORBLIT_REGISTER_CLASS(AnimationNodeObserverBlendSpace);
	ORBLIT_REGISTER_CLASS(AnimationNodeStateMachine);
	ORBLIT_REGISTER_CLASS(AnimationNodeStateMachinePlayback);
	ORBLIT_REGISTER_VIRTUAL_CLASS(AnimationNodeExtension);

	ORBLIT_REGISTER_CLASS(AnimationNodeSync);
	ORBLIT_REGISTER_CLASS(AnimationNodeStateMachineTransition);
	ORBLIT_REGISTER_CLASS(AnimationNodeOutput);
	ORBLIT_REGISTER_CLASS(AnimationNodeOneShot);
	ORBLIT_REGISTER_CLASS(AnimationNodeObserverOneShot);
	ORBLIT_REGISTER_CLASS(AnimationNodeAnimation);
	ORBLIT_REGISTER_CLASS(AnimationNodeAdd2);
	ORBLIT_REGISTER_CLASS(AnimationNodeAdd3);
	ORBLIT_REGISTER_CLASS(AnimationNodeBlend2);
	ORBLIT_REGISTER_CLASS(AnimationNodeBlend3);
	ORBLIT_REGISTER_CLASS(AnimationNodeSub2);
	ORBLIT_REGISTER_CLASS(AnimationNodeTimeScale);
	ORBLIT_REGISTER_CLASS(AnimationNodeTimeSeek);
	ORBLIT_REGISTER_CLASS(AnimationNodeTransition);
	ORBLIT_REGISTER_CLASS(AnimationNodeObserverTransition);

	ORBLIT_REGISTER_CLASS(ShaderGlobalsOverride); // can be used in any shader

	OS::get_singleton()->yield(); // may take time to init

	/* REGISTER 3D */

#ifndef SPATIAL_DISABLED
	ORBLIT_REGISTER_CLASS(Entity3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Entity3DGizmo);
	ORBLIT_REGISTER_CLASS(Skin);
	ORBLIT_REGISTER_ABSTRACT_CLASS(SkinReference);
	ORBLIT_REGISTER_CLASS(Skeleton3D);
	ORBLIT_REGISTER_CLASS(ImporterMesh);
	ORBLIT_REGISTER_CLASS(ImporterMeshInstance3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(VisualInstance3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(GeometryInstance3D);
	ORBLIT_REGISTER_CLASS(Camera3D);
	ORBLIT_REGISTER_CLASS(AudioListener3D);
	ORBLIT_REGISTER_CLASS(MeshInstance3D);
	ORBLIT_REGISTER_CLASS(OccluderInstance3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Occluder3D);
	ORBLIT_REGISTER_CLASS(ArrayOccluder3D);
	ORBLIT_REGISTER_CLASS(QuadOccluder3D);
	ORBLIT_REGISTER_CLASS(BoxOccluder3D);
	ORBLIT_REGISTER_CLASS(SphereOccluder3D);
	ORBLIT_REGISTER_CLASS(PolygonOccluder3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(SpriteBase3D);
	ORBLIT_REGISTER_CLASS(Sprite3D);
	ORBLIT_REGISTER_CLASS(AnimatedSprite3D);
	ORBLIT_REGISTER_CLASS(Label3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Light3D);
	ORBLIT_REGISTER_CLASS(DirectionalLight3D);
	ORBLIT_REGISTER_CLASS(OmniLight3D);
	ORBLIT_REGISTER_CLASS(SpotLight3D);
	ORBLIT_REGISTER_CLASS(AreaLight3D);
	ORBLIT_REGISTER_CLASS(ReflectionProbe);
	ORBLIT_REGISTER_CLASS(Decal);
	ORBLIT_REGISTER_CLASS(VoxelGI);
	ORBLIT_REGISTER_CLASS(VoxelGIData);
	ORBLIT_REGISTER_CLASS(LightmapGI);
	ORBLIT_REGISTER_CLASS(LightmapGIData);
	ORBLIT_REGISTER_CLASS(LightmapProbe);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Lightmapper);

	ORBLIT_REGISTER_ABSTRACT_CLASS(LineBase3D);
	ORBLIT_REGISTER_CLASS(Trail3D);

	ORBLIT_REGISTER_CLASS(GPUParticles3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(GPUParticlesCollision3D);
	ORBLIT_REGISTER_CLASS(GPUParticlesCollisionBox3D);
	ORBLIT_REGISTER_CLASS(GPUParticlesCollisionSphere3D);
	ORBLIT_REGISTER_CLASS(GPUParticlesCollisionSDF3D);
	ORBLIT_REGISTER_CLASS(GPUParticlesCollisionHeightField3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(GPUParticlesAttractor3D);
	ORBLIT_REGISTER_CLASS(GPUParticlesAttractorBox3D);
	ORBLIT_REGISTER_CLASS(GPUParticlesAttractorSphere3D);
	ORBLIT_REGISTER_CLASS(GPUParticlesAttractorVectorField3D);
	ORBLIT_REGISTER_CLASS(CPUParticles3D);
	ORBLIT_REGISTER_CLASS(Marker3D);
	ORBLIT_REGISTER_CLASS(RootMotionView);
	ORBLIT_REGISTER_VIRTUAL_CLASS(SkeletonModifier3D);
	ORBLIT_REGISTER_CLASS(RetargetModifier3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(JointLimitation3D);
	ORBLIT_REGISTER_CLASS(JointLimitationCone3D);
	ORBLIT_REGISTER_CLASS(SpringBoneSimulator3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(SpringBoneCollision3D);
	ORBLIT_REGISTER_CLASS(SpringBoneCollisionSphere3D);
	ORBLIT_REGISTER_CLASS(SpringBoneCollisionCapsule3D);
	ORBLIT_REGISTER_CLASS(SpringBoneCollisionPlane3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(BoneConstraint3D);
	ORBLIT_REGISTER_CLASS(CopyTransformModifier3D);
	ORBLIT_REGISTER_CLASS(ConvertTransformModifier3D);
	ORBLIT_REGISTER_CLASS(AimModifier3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(IKModifier3D);
	ORBLIT_REGISTER_CLASS(TwoBoneIK3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(ChainIK3D);
	ORBLIT_REGISTER_CLASS(SplineIK3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(IterateIK3D);
	ORBLIT_REGISTER_CLASS(FABRIK3D);
	ORBLIT_REGISTER_CLASS(CCDIK3D);
	ORBLIT_REGISTER_CLASS(JacobianIK3D);
	ORBLIT_REGISTER_CLASS(LimitAngularVelocityModifier3D);
	ORBLIT_REGISTER_CLASS(BoneTwistDisperser3D);
	ORBLIT_REGISTER_CLASS(ModifierBoneTarget3D);

#ifndef XR_DISABLED
	ORBLIT_REGISTER_CLASS(XRCamera3D);
	ORBLIT_REGISTER_CLASS(XREntity3D);
	ORBLIT_REGISTER_CLASS(XRController3D);
	ORBLIT_REGISTER_CLASS(XRAnchor3D);
	ORBLIT_REGISTER_CLASS(XROrigin3D);
	ORBLIT_REGISTER_CLASS(XRBodyModifier3D);
	ORBLIT_REGISTER_CLASS(XRHandModifier3D);
	ORBLIT_REGISTER_CLASS(XRFaceModifier3D);
#endif // XR_DISABLED

	OS::get_singleton()->yield(); // may take time to init

#ifndef PHYSICS_SPATIAL_DISABLED
	ORBLIT_REGISTER_ABSTRACT_CLASS(CollisionObject3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(PhysicsBody3D);
	ORBLIT_REGISTER_CLASS(StaticBody3D);
	ORBLIT_REGISTER_CLASS(AnimatableBody3D);
	ORBLIT_REGISTER_CLASS(RigidBody3D);
	ORBLIT_REGISTER_CLASS(KinematicCollision3D);
	ORBLIT_REGISTER_CLASS(CharacterBody3D);
	ORBLIT_REGISTER_CLASS(SpringArm3D);

	ORBLIT_REGISTER_CLASS(PhysicalBoneSimulator3D);
	ORBLIT_REGISTER_CLASS(PhysicalBone3D);
	ORBLIT_REGISTER_CLASS(SoftBody3D);
#endif // PHYSICS_SPATIAL_DISABLED

	ORBLIT_REGISTER_CLASS(BoneSpaceAdjuster3D);
	ORBLIT_REGISTER_CLASS(BoneAttachment3D);
	ORBLIT_REGISTER_CLASS(LookAtModifier3D);

#ifndef PHYSICS_SPATIAL_DISABLED
	ORBLIT_REGISTER_CLASS(VehicleBody3D);
	ORBLIT_REGISTER_CLASS(VehicleWheel3D);
	ORBLIT_REGISTER_CLASS(Area3D);
	ORBLIT_REGISTER_CLASS(CollisionShape3D);
	ORBLIT_REGISTER_CLASS(CollisionPolygon3D);
	ORBLIT_REGISTER_CLASS(RayCast3D);
	ORBLIT_REGISTER_CLASS(ShapeCast3D);
#endif // PHYSICS_SPATIAL_DISABLED
	ORBLIT_REGISTER_CLASS(MultiMeshInstance3D);

	ORBLIT_REGISTER_CLASS(Curve3D);
	ORBLIT_REGISTER_CLASS(Path3D);
	ORBLIT_REGISTER_CLASS(PathFollow3D);
	ORBLIT_REGISTER_CLASS(VisibleOnScreenNotifier3D);
	ORBLIT_REGISTER_CLASS(VisibleOnScreenEnabler3D);
	ORBLIT_REGISTER_CLASS(WorldEnvironment);
	ORBLIT_REGISTER_CLASS(FogVolume);
	ORBLIT_REGISTER_CLASS(FogMaterial);
	ORBLIT_REGISTER_CLASS(RemoteTransform3D);

#ifndef PHYSICS_SPATIAL_DISABLED
	ORBLIT_REGISTER_ABSTRACT_CLASS(Joint3D);
	ORBLIT_REGISTER_CLASS(PinJoint3D);
	ORBLIT_REGISTER_CLASS(HingeJoint3D);
	ORBLIT_REGISTER_CLASS(SliderJoint3D);
	ORBLIT_REGISTER_CLASS(ConeTwistJoint3D);
	ORBLIT_REGISTER_CLASS(Generic6DOFJoint3D);
#endif // PHYSICS_SPATIAL_DISABLED

#ifndef NAVIGATION_SPATIAL_DISABLED
	ORBLIT_REGISTER_CLASS(NavigationMeshSourceGeometryData3D);
	ORBLIT_REGISTER_CLASS(NavigationRegion3D);
	ORBLIT_REGISTER_CLASS(NavigationAgent3D);
	ORBLIT_REGISTER_CLASS(NavigationObstacle3D);
	ORBLIT_REGISTER_CLASS(NavigationLink3D);
#endif // NAVIGATION_SPATIAL_DISABLED

	OS::get_singleton()->yield(); // may take time to init
#endif // SPATIAL_DISABLED

	/* REGISTER SHADER */

	ORBLIT_REGISTER_CLASS(Shader);
	ORBLIT_REGISTER_CLASS(ShaderInclude);

	ORBLIT_REGISTER_CLASS(ShaderMaterial);
	ORBLIT_REGISTER_CLASS(CanvasTexture);
	ORBLIT_REGISTER_CLASS(CanvasItemMaterial);

	Stage::add_idle_callback(CanvasItemMaterial::flush_changes);
	CanvasItemMaterial::init_shaders();
	ORBLIT_REGISTER_CLASS(BlitMaterial);

	// SpriteFrames is needed by both AnimatedSprite2D and AnimatedSprite3D.
	ORBLIT_REGISTER_CLASS(SpriteFrames);

	/* REGISTER 2D */

#ifndef PLANAR_DISABLED
	ORBLIT_REGISTER_CLASS(Entity2D);
	ORBLIT_REGISTER_CLASS(CanvasGroup);
	ORBLIT_REGISTER_CLASS(CPUParticles2D);
	ORBLIT_REGISTER_CLASS(GPUParticles2D);
	ORBLIT_REGISTER_CLASS(Sprite2D);
	ORBLIT_REGISTER_CLASS(AnimatedSprite2D);
	ORBLIT_REGISTER_CLASS(Marker2D);
	ORBLIT_REGISTER_CLASS(Line2D);
	ORBLIT_REGISTER_CLASS(MeshInstance2D);
	ORBLIT_REGISTER_CLASS(MultiMeshInstance2D);
#ifndef PHYSICS_PLANAR_DISABLED
	ORBLIT_REGISTER_ABSTRACT_CLASS(CollisionObject2D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(PhysicsBody2D);
	ORBLIT_REGISTER_CLASS(StaticBody2D);
	ORBLIT_REGISTER_CLASS(AnimatableBody2D);
	ORBLIT_REGISTER_CLASS(RigidBody2D);
	ORBLIT_REGISTER_CLASS(CharacterBody2D);
	ORBLIT_REGISTER_CLASS(KinematicCollision2D);
	ORBLIT_REGISTER_CLASS(Area2D);
	ORBLIT_REGISTER_CLASS(CollisionShape2D);
	ORBLIT_REGISTER_CLASS(CollisionPolygon2D);
	ORBLIT_REGISTER_CLASS(RayCast2D);
	ORBLIT_REGISTER_CLASS(ShapeCast2D);
#endif // PHYSICS_PLANAR_DISABLED
	ORBLIT_REGISTER_CLASS(VisibleOnScreenNotifier2D);
	ORBLIT_REGISTER_CLASS(VisibleOnScreenEnabler2D);
	ORBLIT_REGISTER_CLASS(Polygon2D);
	ORBLIT_REGISTER_CLASS(Skeleton2D);
	ORBLIT_REGISTER_CLASS(Bone2D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Light2D);
	ORBLIT_REGISTER_CLASS(PointLight2D);
	ORBLIT_REGISTER_CLASS(DirectionalLight2D);
	ORBLIT_REGISTER_CLASS(LightOccluder2D);
	ORBLIT_REGISTER_CLASS(OccluderPolygon2D);
	ORBLIT_REGISTER_CLASS(BackBufferCopy);
	ORBLIT_REGISTER_CLASS(CanvasModulate);

	OS::get_singleton()->yield(); // may take time to init

	ORBLIT_REGISTER_CLASS(Camera2D);
	ORBLIT_REGISTER_CLASS(AudioListener2D);
#ifndef PHYSICS_PLANAR_DISABLED
	ORBLIT_REGISTER_ABSTRACT_CLASS(Joint2D);
	ORBLIT_REGISTER_CLASS(PinJoint2D);
	ORBLIT_REGISTER_CLASS(GrooveJoint2D);
	ORBLIT_REGISTER_CLASS(DampedSpringJoint2D);
	ORBLIT_REGISTER_CLASS(TouchScreenButton);
#endif // PHYSICS_PLANAR_DISABLED
	ORBLIT_REGISTER_CLASS(Parallax2D);
	ORBLIT_REGISTER_CLASS(RemoteTransform2D);

	ORBLIT_REGISTER_CLASS(SkeletonModificationStack2D);
	ORBLIT_REGISTER_CLASS(SkeletonModification2D);
	ORBLIT_REGISTER_CLASS(SkeletonModification2DLookAt);
	ORBLIT_REGISTER_CLASS(SkeletonModification2DCCDIK);
	ORBLIT_REGISTER_CLASS(SkeletonModification2DFABRIK);
	ORBLIT_REGISTER_CLASS(SkeletonModification2DTwoBoneIK);
	ORBLIT_REGISTER_CLASS(SkeletonModification2DStackHolder);

#ifndef PHYSICS_PLANAR_DISABLED
	ORBLIT_REGISTER_CLASS(PhysicalBone2D);
	ORBLIT_REGISTER_CLASS(SkeletonModification2DJiggle);
	ORBLIT_REGISTER_CLASS(SkeletonModification2DPhysicalBones);
#endif // PHYSICS_PLANAR_DISABLED
#endif // PLANAR_DISABLED

	OS::get_singleton()->yield(); // may take time to init

	/* REGISTER RESOURCES */

	ORBLIT_REGISTER_ABSTRACT_CLASS(Shader);
	ORBLIT_REGISTER_CLASS(ParticleProcessMaterial);
	Stage::add_idle_callback(ParticleProcessMaterial::flush_changes);
	ParticleProcessMaterial::init_shaders();

	ORBLIT_REGISTER_VIRTUAL_CLASS(Mesh);
	ORBLIT_REGISTER_CLASS(MeshConvexDecompositionSettings);
	ORBLIT_REGISTER_CLASS(ArrayMesh);
	ORBLIT_REGISTER_CLASS(PlaceholderMesh);
	ORBLIT_REGISTER_CLASS(ImmediateMesh);
	ORBLIT_REGISTER_CLASS(MultiMesh);
	ORBLIT_REGISTER_CLASS(SurfaceTool);
	ORBLIT_REGISTER_CLASS(MeshDataTool);

#ifndef SPATIAL_DISABLED
	ORBLIT_REGISTER_CLASS(AudioStreamPlayer3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(PrimitiveMesh);
	ORBLIT_REGISTER_CLASS(BoxMesh);
	ORBLIT_REGISTER_CLASS(CapsuleMesh);
	ORBLIT_REGISTER_CLASS(CylinderMesh);
	ORBLIT_REGISTER_CLASS(PlaneMesh);
	ORBLIT_REGISTER_CLASS(PrismMesh);
	ORBLIT_REGISTER_CLASS(QuadMesh);
	ORBLIT_REGISTER_CLASS(SphereMesh);
	ORBLIT_REGISTER_CLASS(TextMesh);
	ORBLIT_REGISTER_CLASS(TorusMesh);
	ORBLIT_REGISTER_CLASS(TubeTrailMesh);
	ORBLIT_REGISTER_CLASS(RibbonTrailMesh);
	ORBLIT_REGISTER_CLASS(PointMesh);
	ORBLIT_REGISTER_ABSTRACT_CLASS(BaseMaterial3D);
	ORBLIT_REGISTER_CLASS(StandardMaterial3D);
	ORBLIT_REGISTER_CLASS(ORMMaterial3D);
	ORBLIT_REGISTER_CLASS(ProceduralSkyMaterial);
	ORBLIT_REGISTER_CLASS(PanoramaSkyMaterial);
	ORBLIT_REGISTER_CLASS(PhysicalSkyMaterial);
	Stage::add_idle_callback(BaseMaterial3D::flush_changes);
	BaseMaterial3D::init_shaders();

	ORBLIT_REGISTER_CLASS(MeshLibrary);

	OS::get_singleton()->yield(); // may take time to init

#ifndef PHYSICS_SPATIAL_DISABLED
	ORBLIT_REGISTER_ABSTRACT_CLASS(Shape3D);
	ORBLIT_REGISTER_CLASS(SeparationRayShape3D);
	ORBLIT_REGISTER_CLASS(SphereShape3D);
	ORBLIT_REGISTER_CLASS(BoxShape3D);
	ORBLIT_REGISTER_CLASS(CapsuleShape3D);
	ORBLIT_REGISTER_CLASS(CylinderShape3D);
	ORBLIT_REGISTER_CLASS(HeightMapShape3D);
	ORBLIT_REGISTER_CLASS(WorldBoundaryShape3D);
	ORBLIT_REGISTER_CLASS(ConvexPolygonShape3D);
	ORBLIT_REGISTER_CLASS(ConcavePolygonShape3D);
#endif // PHYSICS_SPATIAL_DISABLED
	ORBLIT_REGISTER_CLASS(World3D);

	OS::get_singleton()->yield(); // may take time to init
#endif // SPATIAL_DISABLED

#if !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)
	ORBLIT_REGISTER_CLASS(PhysicsMaterial);
#endif // !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)
	ORBLIT_REGISTER_CLASS(Compositor);
	ORBLIT_REGISTER_CLASS(Environment);
	ORBLIT_REGISTER_VIRTUAL_CLASS(CameraAttributes);
	ORBLIT_REGISTER_CLASS(CameraAttributesPhysical);
	ORBLIT_REGISTER_CLASS(CameraAttributesPractical);
	ORBLIT_REGISTER_CLASS(Curve2D);
	ORBLIT_REGISTER_CLASS(World2D);
	ORBLIT_REGISTER_CLASS(Sky);
	ORBLIT_REGISTER_CLASS(CompressedTexture2D);
	ORBLIT_REGISTER_CLASS(StreamedTexture2D);
	ORBLIT_REGISTER_CLASS(PortableCompressedTexture2D);
	ORBLIT_REGISTER_CLASS(ImageTexture);
	ORBLIT_REGISTER_CLASS(AtlasTexture);
	ORBLIT_REGISTER_CLASS(MeshTexture);
	ORBLIT_REGISTER_CLASS(CurveTexture);
	ORBLIT_REGISTER_CLASS(CurveXYZTexture);
	ORBLIT_REGISTER_CLASS(GradientTexture1D);
	ORBLIT_REGISTER_CLASS(GradientTexture2D);
	ORBLIT_REGISTER_CLASS(CameraTexture);
	ORBLIT_REGISTER_CLASS(ExternalTexture);
	ORBLIT_REGISTER_CLASS(DrawableTexture2D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(TextureLayered);
	ORBLIT_REGISTER_ABSTRACT_CLASS(ImageTextureLayered);
	ORBLIT_REGISTER_VIRTUAL_CLASS(Texture3D);
	ORBLIT_REGISTER_CLASS(ImageTexture3D);
	ORBLIT_REGISTER_CLASS(CompressedTexture3D);
	ORBLIT_REGISTER_CLASS(Cubemap);
	ORBLIT_REGISTER_CLASS(CubemapArray);
	ORBLIT_REGISTER_CLASS(Texture2DArray);
	ORBLIT_REGISTER_ABSTRACT_CLASS(CompressedTextureLayered);
	ORBLIT_REGISTER_CLASS(CompressedCubemap);
	ORBLIT_REGISTER_CLASS(CompressedCubemapArray);
	ORBLIT_REGISTER_CLASS(CompressedTexture2DArray);
	ORBLIT_REGISTER_CLASS(PlaceholderTexture2D);
	ORBLIT_REGISTER_CLASS(PlaceholderTexture3D);
	ORBLIT_REGISTER_ABSTRACT_CLASS(PlaceholderTextureLayered);
	ORBLIT_REGISTER_CLASS(PlaceholderTexture2DArray);
	ORBLIT_REGISTER_CLASS(PlaceholderCubemap);
	ORBLIT_REGISTER_CLASS(PlaceholderCubemapArray);
	ORBLIT_REGISTER_CLASS(DPITexture);

#ifdef RD_ENABLED
	// These classes are part of renderer_rd.
	ORBLIT_REGISTER_CLASS(Texture2DRD);
	ORBLIT_REGISTER_ABSTRACT_CLASS(TextureLayeredRD);
	ORBLIT_REGISTER_CLASS(Texture2DArrayRD);
	ORBLIT_REGISTER_CLASS(TextureCubemapRD);
	ORBLIT_REGISTER_CLASS(TextureCubemapArrayRD);
	ORBLIT_REGISTER_CLASS(Texture3DRD);
#endif // RD_ENABLED

	ORBLIT_REGISTER_CLASS(Animation);
	ORBLIT_REGISTER_CLASS(AnimationLibrary);

	ORBLIT_REGISTER_ABSTRACT_CLASS(Font);
	ORBLIT_REGISTER_CLASS(FontFile);
	ORBLIT_REGISTER_CLASS(FontVariation);
	ORBLIT_REGISTER_CLASS(SystemFont);
	ORBLIT_REGISTER_CLASS(ColorPalette);

	ORBLIT_REGISTER_CLASS(Curve);

	ORBLIT_REGISTER_CLASS(LabelSettings);

	ORBLIT_REGISTER_CLASS(TextLine);
	ORBLIT_REGISTER_CLASS(TextParagraph);

	ORBLIT_REGISTER_VIRTUAL_CLASS(StyleBox);
	ORBLIT_REGISTER_CLASS(StyleBoxEmpty);
	ORBLIT_REGISTER_CLASS(StyleBoxTexture);
	ORBLIT_REGISTER_CLASS(StyleBoxFlat);
	ORBLIT_REGISTER_CLASS(StyleBoxLine);
	ORBLIT_REGISTER_CLASS(Theme);

	ORBLIT_REGISTER_CLASS(BitMap);
	ORBLIT_REGISTER_CLASS(Gradient);

	ORBLIT_REGISTER_CLASS(SkeletonProfile);
	ORBLIT_REGISTER_CLASS(SkeletonProfileHumanoid);
	ORBLIT_REGISTER_CLASS(BoneMap);

	OS::get_singleton()->yield(); // may take time to init

	ORBLIT_REGISTER_CLASS(AudioStream);
	ORBLIT_REGISTER_CLASS(AudioStreamPlayback);
	ORBLIT_REGISTER_VIRTUAL_CLASS(AudioStreamPlaybackResampled);
	ORBLIT_REGISTER_CLASS(AudioStreamPlayer);
	ORBLIT_REGISTER_CLASS(AudioStreamWAV);
	ORBLIT_REGISTER_CLASS(AudioStreamMicrophone);
	ORBLIT_REGISTER_CLASS(AudioStreamPolyphonic);
	ORBLIT_REGISTER_CLASS(AudioStreamRandomizer);
	ORBLIT_REGISTER_CLASS(AudioStreamGenerator);
	ORBLIT_REGISTER_ABSTRACT_CLASS(AudioStreamGeneratorPlayback);
	ORBLIT_REGISTER_ABSTRACT_CLASS(AudioStreamPlaybackPolyphonic);

	OS::get_singleton()->yield(); // may take time to init

#ifndef PLANAR_DISABLED
	ORBLIT_REGISTER_CLASS(AudioStreamPlayer2D);
	ORBLIT_REGISTER_CLASS(Path2D);
	ORBLIT_REGISTER_CLASS(PathFollow2D);

#ifndef PHYSICS_PLANAR_DISABLED
	ORBLIT_REGISTER_ABSTRACT_CLASS(Shape2D);
	ORBLIT_REGISTER_CLASS(WorldBoundaryShape2D);
	ORBLIT_REGISTER_CLASS(SegmentShape2D);
	ORBLIT_REGISTER_CLASS(SeparationRayShape2D);
	ORBLIT_REGISTER_CLASS(CircleShape2D);
	ORBLIT_REGISTER_CLASS(RectangleShape2D);
	ORBLIT_REGISTER_CLASS(CapsuleShape2D);
	ORBLIT_REGISTER_CLASS(ConvexPolygonShape2D);
	ORBLIT_REGISTER_CLASS(ConcavePolygonShape2D);
#endif // PHYSICS_PLANAR_DISABLED

#if !defined(NAVIGATION_PLANAR_DISABLED) || !defined(NAVIGATION_SPATIAL_DISABLED)
	ORBLIT_REGISTER_CLASS(NavigationMesh);
#endif // !defined(NAVIGATION_PLANAR_DISABLED) || !defined(NAVIGATION_SPATIAL_DISABLED)

#ifndef NAVIGATION_PLANAR_DISABLED
	ORBLIT_REGISTER_CLASS(NavigationMeshSourceGeometryData2D);
	ORBLIT_REGISTER_CLASS(NavigationPolygon);
	ORBLIT_REGISTER_CLASS(NavigationRegion2D);
	ORBLIT_REGISTER_CLASS(NavigationAgent2D);
	ORBLIT_REGISTER_CLASS(NavigationObstacle2D);
	ORBLIT_REGISTER_CLASS(NavigationLink2D);
	ORBLIT_REGISTER_CLASS(PolygonPathFinder);

	OS::get_singleton()->yield(); // may take time to init

	// 2D nodes that support navmesh baking need to server register their source geometry parsers.
	MeshInstance2D::navmesh_parse_init();
	MultiMeshInstance2D::navmesh_parse_init();
	NavigationObstacle2D::navmesh_parse_init();
	Polygon2D::navmesh_parse_init();
#ifndef PHYSICS_PLANAR_DISABLED
	StaticBody2D::navmesh_parse_init();
#endif // PHYSICS_PLANAR_DISABLED
#endif // NAVIGATION_PLANAR_DISABLED
#endif // PLANAR_DISABLED

#ifndef NAVIGATION_SPATIAL_DISABLED
	// 3D nodes that support navmesh baking need to server register their source geometry parsers.
	MeshInstance3D::navmesh_parse_init();
	MultiMeshInstance3D::navmesh_parse_init();
	NavigationObstacle3D::navmesh_parse_init();
#ifndef PHYSICS_SPATIAL_DISABLED
	StaticBody3D::navmesh_parse_init();
#endif // PHYSICS_SPATIAL_DISABLED
#endif // NAVIGATION_SPATIAL_DISABLED

#if !defined(NAVIGATION_PLANAR_DISABLED) || !defined(NAVIGATION_SPATIAL_DISABLED)
	OS::get_singleton()->yield(); // may take time to init
#endif // !defined(NAVIGATION_PLANAR_DISABLED) || !defined(NAVIGATION_SPATIAL_DISABLED)

	ORBLIT_REGISTER_ABSTRACT_CLASS(SceneState);
	ORBLIT_REGISTER_CLASS(Prefab);

	ORBLIT_REGISTER_CLASS(Stage);
	ORBLIT_REGISTER_ABSTRACT_CLASS(StageTimer); // sorry, you can't create it

	OS::get_singleton()->yield(); // may take time to init

	for (int i = 0; i < 20; i++) {
		GLOBAL_DEF_BASIC(vformat("%s/layer_%d", PNAME("layer_names/planar_render"), i + 1), "");
		GLOBAL_DEF_BASIC(vformat("%s/layer_%d", PNAME("layer_names/spatial_render"), i + 1), "");
	}

	for (int i = 0; i < 32; i++) {
		GLOBAL_DEF_BASIC(vformat("%s/layer_%d", PNAME("layer_names/planar_physics"), i + 1), "");
#ifndef NAVIGATION_PLANAR_DISABLED
		GLOBAL_DEF_BASIC(vformat("%s/layer_%d", PNAME("layer_names/planar_navigation"), i + 1), "");
#endif // NAVIGATION_PLANAR_DISABLED
		GLOBAL_DEF_BASIC(vformat("%s/layer_%d", PNAME("layer_names/spatial_physics"), i + 1), "");
#ifndef NAVIGATION_SPATIAL_DISABLED
		GLOBAL_DEF_BASIC(vformat("%s/layer_%d", PNAME("layer_names/spatial_navigation"), i + 1), "");
#endif // NAVIGATION_SPATIAL_DISABLED
	}

	for (int i = 0; i < 32; i++) {
		GLOBAL_DEF_BASIC(vformat("%s/layer_%d", PNAME("layer_names/avoidance"), i + 1), "");
	}

	if (RenderingSystem::get_singleton()) {
		// RenderingSystem needs to exist for this to succeed.
		ColorPickerShape::init_shaders();
		GraphEdit::init_shaders();
#ifndef SPATIAL_DISABLED
		Trail3D::init_shaders();
#endif //SPATIAL_DISABLED
	}

	SceneDebugger::initialize();

	OS::get_singleton()->benchmark_end_measure("Scene", "Register Types");
}

void unregister_scene_types() {
	OS::get_singleton()->benchmark_begin_measure("Scene", "Unregister Types");

	SceneDebugger::deinitialize();

	if constexpr (ORBLIT_IS_CLASS_ENABLED(TextureLayered)) {
		ResourceLoader::remove_resource_format_loader(resource_loader_texture_layered);
		resource_loader_texture_layered.unref();
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Texture3D)) {
		ResourceLoader::remove_resource_format_loader(resource_loader_texture_3d);
		resource_loader_texture_3d.unref();
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(CompressedTexture2D)) {
		ResourceLoader::remove_resource_format_loader(resource_loader_compressed_texture);
		resource_loader_compressed_texture.unref();
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(StreamedTexture2D)) {
		ResourceLoader::remove_resource_format_loader(resource_loader_streamed_texture);
		resource_loader_streamed_texture.unref();
	}

	ResourceSaver::remove_resource_format_saver(resource_saver_text);
	resource_saver_text.unref();

	ResourceLoader::remove_resource_format_loader(resource_loader_text);
	resource_loader_text.unref();

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Shader)) {
		ResourceSaver::remove_resource_format_saver(resource_saver_shader);
		resource_saver_shader.unref();

		ResourceLoader::remove_resource_format_loader(resource_loader_shader);
		resource_loader_shader.unref();
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(ShaderInclude)) {
		ResourceSaver::remove_resource_format_saver(resource_saver_shader_include);
		resource_saver_shader_include.unref();

		ResourceLoader::remove_resource_format_loader(resource_loader_shader_include);
		resource_loader_shader_include.unref();
	}

	// StandardMaterial3D is not initialized when 3D is disabled, so it shouldn't be cleaned up either
#ifndef SPATIAL_DISABLED
	BaseMaterial3D::finish_shaders();
	PhysicalSkyMaterial::cleanup_shader();
	PanoramaSkyMaterial::cleanup_shader();
	ProceduralSkyMaterial::cleanup_shader();
	FogMaterial::cleanup_shader();
	Trail3D::finish_shaders();
#endif // SPATIAL_DISABLED

	ParticleProcessMaterial::finish_shaders();
	CanvasItemMaterial::finish_shaders();
	ColorPickerShape::finish_shaders();
	BlitMaterial::cleanup_shader();
	GraphEdit::finish_shaders();
	SceneStringNames::free();

	OS::get_singleton()->benchmark_end_measure("Scene", "Unregister Types");
}

void register_scene_singletons() {
	OS::get_singleton()->benchmark_begin_measure("Scene", "Register Singletons");

	ORBLIT_REGISTER_CLASS(ThemeDB);

	Engine::get_singleton()->add_singleton(Engine::Singleton("ThemeDB", ThemeDB::get_singleton()));

	OS::get_singleton()->benchmark_end_measure("Scene", "Register Singletons");
}
