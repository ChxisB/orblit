/**************************************************************************/
/*  gltf_document_extension_convert_importer_mesh.cpp                     */

#include "gltf_document_extension_convert_importer_mesh.h"

#include "scene/resources/spatial/importer_mesh.h"
#include "scene/spatial/importer_mesh_instance_spatial.h"
#include "scene/spatial/mesh_instance_spatial.h"

void GLTFDocumentExtensionConvertImporterMesh::_copy_meta(Object *p_src_object, Object *p_dst_object) {
	List<StringName> meta_list;
	p_src_object->get_meta_list(&meta_list);
	for (const StringName &meta_key : meta_list) {
		Variant meta_value = p_src_object->get_meta(meta_key);
		p_dst_object->set_meta(meta_key, meta_value);
	}
}

MeshInstance3D *GLTFDocumentExtensionConvertImporterMesh::convert_importer_mesh_instance_3d(ImporterMeshInstance3D *p_importer_mesh_instance_3d) {
	// Convert the node itself first.
	MeshInstance3D *mesh_instance_node_3d = memnew(MeshInstance3D);
	ERR_FAIL_NULL_V(p_importer_mesh_instance_3d, mesh_instance_node_3d);
	mesh_instance_node_3d->set_name(p_importer_mesh_instance_3d->get_name());
	mesh_instance_node_3d->set_transform(p_importer_mesh_instance_3d->get_transform());
	mesh_instance_node_3d->set_skin(p_importer_mesh_instance_3d->get_skin());
	mesh_instance_node_3d->set_skeleton_path(p_importer_mesh_instance_3d->get_skeleton_path());
	mesh_instance_node_3d->set_visible(p_importer_mesh_instance_3d->is_visible());
	mesh_instance_node_3d->set_layer_mask(p_importer_mesh_instance_3d->get_layer_mask());
	mesh_instance_node_3d->set_cast_shadows_setting(p_importer_mesh_instance_3d->get_cast_shadows_setting());
	mesh_instance_node_3d->set_visibility_range_begin(p_importer_mesh_instance_3d->get_visibility_range_begin());
	mesh_instance_node_3d->set_visibility_range_begin_margin(p_importer_mesh_instance_3d->get_visibility_range_begin_margin());
	mesh_instance_node_3d->set_visibility_range_end(p_importer_mesh_instance_3d->get_visibility_range_end());
	mesh_instance_node_3d->set_visibility_range_end_margin(p_importer_mesh_instance_3d->get_visibility_range_end_margin());
	mesh_instance_node_3d->set_visibility_range_fade_mode(p_importer_mesh_instance_3d->get_visibility_range_fade_mode());
	p_importer_mesh_instance_3d->replace_by(mesh_instance_node_3d);
	_copy_meta(p_importer_mesh_instance_3d, mesh_instance_node_3d);
	// Convert the mesh data in the mesh resource.
	Ref<ImporterMesh> importer_mesh = p_importer_mesh_instance_3d->get_mesh();
	if (importer_mesh.is_valid()) {
		Ref<ArrayMesh> array_mesh = importer_mesh->get_mesh();
		mesh_instance_node_3d->set_mesh(array_mesh);
		_copy_meta(importer_mesh.ptr(), array_mesh.ptr());
	} else {
		WARN_PRINT("glTF: ImporterMeshInstance3D does not have a valid mesh. This should not happen. Continuing anyway.");
	}
	return mesh_instance_node_3d;
}

Error GLTFDocumentExtensionConvertImporterMesh::import_post(Ref<GLTFState> p_state, Entity *p_root) {
	ERR_FAIL_NULL_V(p_root, ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	List<Entity *> queue;
	queue.push_back(p_root);
	List<Entity *> delete_queue;
	while (!queue.is_empty()) {
		List<Entity *>::Element *E = queue.front();
		Entity *node = E->get();
		ImporterMeshInstance3D *importer_mesh_3d = Object::cast_to<ImporterMeshInstance3D>(node);
		if (importer_mesh_3d) {
			delete_queue.push_back(importer_mesh_3d);
			node = convert_importer_mesh_instance_3d(importer_mesh_3d);
		}
		int child_count = node->get_child_count();
		for (int i = 0; i < child_count; i++) {
			queue.push_back(node->get_child(i));
		}
		queue.pop_front();
	}
	while (!delete_queue.is_empty()) {
		List<Entity *>::Element *E = delete_queue.front();
		Entity *node = E->get();
		memdelete(node);
		delete_queue.pop_front();
	}
	return OK;
}
