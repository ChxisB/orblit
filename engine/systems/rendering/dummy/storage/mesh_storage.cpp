/**************************************************************************/
/*  mesh_storage.cpp                                                      */

#include "mesh_storage.h"

using namespace RendererDummy;

MeshStorage *MeshStorage::singleton = nullptr;

MeshStorage::MeshStorage() {
	singleton = this;
}

MeshStorage::~MeshStorage() {
	singleton = nullptr;
}

RID MeshStorage::mesh_allocate() {
	return mesh_owner.allocate_rid();
}

void MeshStorage::mesh_initialize(RID p_rid) {
	mesh_owner.initialize_rid(p_rid, DummyMesh());
}

void MeshStorage::mesh_free(RID p_rid) {
	DummyMesh *mesh = mesh_owner.get_or_null(p_rid);
	ERR_FAIL_NULL(mesh);
	mesh->dependency.deleted_notify(p_rid);
	mesh_owner.free(p_rid);
}

void MeshStorage::mesh_surface_remove(RID p_mesh, int p_surface) {
	DummyMesh *m = mesh_owner.get_or_null(p_mesh);
	ERR_FAIL_NULL(m);
	m->dependency.changed_notify(Dependency::DEPENDENCY_CHANGED_MESH);
	m->surfaces.remove_at(p_surface);
}

void MeshStorage::mesh_clear(RID p_mesh) {
	DummyMesh *m = mesh_owner.get_or_null(p_mesh);
	ERR_FAIL_NULL(m);

	m->surfaces.clear();
}

RID MeshStorage::_multimesh_allocate() {
	return multimesh_owner.allocate_rid();
}

void MeshStorage::_multimesh_initialize(RID p_rid) {
	multimesh_owner.initialize_rid(p_rid, DummyMultiMesh());
}

void MeshStorage::_multimesh_free(RID p_rid) {
	DummyMultiMesh *multimesh = multimesh_owner.get_or_null(p_rid);
	ERR_FAIL_NULL(multimesh);

	multimesh_owner.free(p_rid);
}

void MeshStorage::_multimesh_set_buffer(RID p_multimesh, const Vector<float> &p_buffer) {
	DummyMultiMesh *multimesh = multimesh_owner.get_or_null(p_multimesh);
	ERR_FAIL_NULL(multimesh);
	multimesh->buffer.resize(p_buffer.size());
	float *cache_data = multimesh->buffer.ptrw();
	memcpy(cache_data, p_buffer.ptr(), p_buffer.size() * sizeof(float));
}

Vector<float> MeshStorage::_multimesh_get_buffer(RID p_multimesh) const {
	DummyMultiMesh *multimesh = multimesh_owner.get_or_null(p_multimesh);
	ERR_FAIL_NULL_V(multimesh, Vector<float>());

	return multimesh->buffer;
}
