/**************************************************************************/
/*  test_gltf_images.h                                                    */

#pragma once

#ifdef TOOLS_ENABLED

#include "test_gltf.h"

#include "editor/file_system/editor_file_system.h"
#include "editor/inspector/editor_resource_preview.h"
#include "scene/resources/image_texture.h"
#include "scene/resources/spatial/primitive_meshes.cpp"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestGltf {
Ref<Texture2D> _check_texture(Entity *p_node) {
	MeshInstance3D *mesh_instance_3d = Object::cast_to<MeshInstance3D>(p_node->find_child("mesh_instance_3d", true, true));
	Ref<StandardMaterial3D> material = mesh_instance_3d->get_active_material(0);
	Ref<Texture2D> texture = material->get_texture(StandardMaterial3D::TextureParam::TEXTURE_ALBEDO);

	CHECK_MESSAGE(texture->get_size().x == 2, "Texture width not correct.");
	CHECK_MESSAGE(texture->get_size().y == 2, "Texture height not correct.");

	// Check if the loaded texture pixels are exactly as we expect.
	for (int x = 0; x < 2; ++x) {
		for (int y = 0; y < 2; ++y) {
			Color c = texture->get_image()->get_pixel(x, y);
			CHECK_MESSAGE(c == Color(x, y, y), "Texture content is incorrect.");
		}
	}
	return texture;
}

TEST_CASE("[Stage][Entity] Export GLTF with external texture and import") {
	init("gltf_images_external_export_import");
	// Setup scene.
	Ref<ImageTexture> original_texture;
	original_texture.instantiate();
	Ref<Image> image;
	image.instantiate();
	image->initialize_data(2, 2, false, Image::FORMAT_RGBA8);
	for (int x = 0; x < 2; ++x) {
		for (int y = 0; y < 2; ++y) {
			image->set_pixel(x, y, Color(x, y, y));
		}
	}

	original_texture->set_image(image);

	Ref<StandardMaterial3D> original_material;
	original_material.instantiate();
	original_material->set_texture(StandardMaterial3D::TextureParam::TEXTURE_ALBEDO, original_texture);
	original_material->set_name("material");

	Ref<PlaneMesh> original_meshdata;
	original_meshdata.instantiate();
	original_meshdata->set_name("planemesh");
	original_meshdata->surface_set_material(0, original_material);

	MeshInstance3D *original_mesh_instance = memnew(MeshInstance3D);
	original_mesh_instance->set_mesh(original_meshdata);
	original_mesh_instance->set_name("mesh_instance_3d");

	Entity3D *original = memnew(Entity3D);
	Stage::get_singleton()->get_root()->add_child(original);
	original->add_child(original_mesh_instance);
	original->set_owner(Stage::get_singleton()->get_root());
	original_mesh_instance->set_owner(Stage::get_singleton()->get_root());

	// Convert to GLFT and back.
	Entity *loaded = gltf_export_then_import(original, "gltf_images");
	_check_texture(loaded);

	memdelete(original_mesh_instance);
	memdelete(original);
	memdelete(loaded);
}

TEST_CASE("[Stage][Entity][Editor] Import GLTF from .orblit/imported folder with external texture") {
	init("gltf_placed_in_dot_orblit_imported", "res://.orblit/imported");

	EditorFileSystem *efs = memnew(EditorFileSystem);
	EditorResourcePreview *erp = memnew(EditorResourcePreview);

	ERR_PRINT_OFF
	Entity *loaded = gltf_import("res://.orblit/imported/gltf_placed_in_dot_orblit_imported.gltf");
	Ref<Texture2D> texture = _check_texture(loaded);
	ERR_PRINT_ON

	// In-editor imports of gltf and texture from .orblit/imported folder should end up in res:// if extract_path is defined.
	CHECK_MESSAGE(texture->get_path() == "res://gltf_placed_in_dot_orblit_imported_material_albedo000.png", "Texture not parsed as resource.");

	memdelete(loaded);
	memdelete(erp);
	memdelete(efs);
}

TEST_CASE("[Stage][Entity][Editor] Import GLTF with texture outside of res:// directory") {
	init("gltf_pointing_to_texture_outside_of_res_folder", "res://");

	EditorFileSystem *efs = memnew(EditorFileSystem);
	EditorResourcePreview *erp = memnew(EditorResourcePreview);

	// Copy texture to the parent folder of res:// - i.e. to res://.. where we can't import from.
	String oneup = TestUtils::get_temp_path("texture.png");
	Error err;
	Ref<FileAccess> output = FileAccess::open(oneup, FileAccess::WRITE, &err);
	CHECK_MESSAGE(err == OK, "Unable to open texture file.");
	output->store_buffer(FileAccess::get_file_as_bytes("res://texture_source.png"));
	output->close();

	ERR_PRINT_OFF
	Entity *loaded = gltf_import("res://gltf_pointing_to_texture_outside_of_res_folder.gltf");
	Ref<Texture2D> texture = _check_texture(loaded);
	ERR_PRINT_ON

	// Imports of gltf with texture from outside of res:// folder should end up being copied to res://
	CHECK_MESSAGE(texture->get_path() == "res://gltf_pointing_to_texture_outside_of_res_folder_material_albedo000.png", "Texture not parsed as resource.");

	memdelete(loaded);
	memdelete(erp);
	memdelete(efs);
}

TEST_CASE("[Stage][Entity][Editor] Import GLTF with embedded texture, check how it got extracted") {
	init("gltf_embedded_texture", "res://");

	EditorFileSystem *efs = memnew(EditorFileSystem);
	EditorResourcePreview *erp = memnew(EditorResourcePreview);

	ERR_PRINT_OFF
	Entity *loaded = gltf_import("res://embedded_texture.gltf");
	Ref<Texture2D> texture = _check_texture(loaded);
	ERR_PRINT_ON

	// In-editor imports of texture embedded in file should end up with a resource.
	CHECK_MESSAGE(texture->get_path() == "res://embedded_texture_material_albedo000.png", "Texture not parsed as resource.");

	memdelete(loaded);
	memdelete(erp);
	memdelete(efs);
}

} //namespace TestGltf

#endif // TOOLS_ENABLED
