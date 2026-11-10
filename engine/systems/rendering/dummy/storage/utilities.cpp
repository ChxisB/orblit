/**************************************************************************/
/*  utilities.cpp                                                         */

#include "utilities.h"

#include "systems/rendering/dummy/storage/light_storage.h"
#include "systems/rendering/dummy/storage/material_storage.h"
#include "systems/rendering/dummy/storage/mesh_storage.h"
#include "systems/rendering/dummy/storage/texture_storage.h"

using namespace RendererDummy;

Utilities *Utilities::singleton = nullptr;

RSE::InstanceType Utilities::get_base_type(RID p_rid) const {
	if (RendererDummy::MeshStorage::get_singleton()->owns_mesh(p_rid)) {
		return RSE::INSTANCE_MESH;
	} else if (RendererDummy::MeshStorage::get_singleton()->owns_multimesh(p_rid)) {
		return RSE::INSTANCE_MULTIMESH;
	} else if (RendererDummy::LightStorage::get_singleton()->owns_lightmap(p_rid)) {
		return RSE::INSTANCE_LIGHTMAP;
	}
	return RSE::INSTANCE_NONE;
}

bool Utilities::free(RID p_rid) {
	if (RendererDummy::LightStorage::get_singleton()->free(p_rid)) {
		return true;
	} else if (RendererDummy::TextureStorage::get_singleton()->owns_texture(p_rid)) {
		RendererDummy::TextureStorage::get_singleton()->texture_free(p_rid);
		return true;
	} else if (RendererDummy::MeshStorage::get_singleton()->owns_mesh(p_rid)) {
		RendererDummy::MeshStorage::get_singleton()->mesh_free(p_rid);
		return true;
	} else if (RendererDummy::MeshStorage::get_singleton()->owns_multimesh(p_rid)) {
		RendererDummy::MeshStorage::get_singleton()->multimesh_free(p_rid);
		return true;
	} else if (RendererDummy::MaterialStorage::get_singleton()->owns_shader(p_rid)) {
		RendererDummy::MaterialStorage::get_singleton()->shader_free(p_rid);
		return true;
	} else if (RendererDummy::MaterialStorage::get_singleton()->owns_material(p_rid)) {
		RendererDummy::MaterialStorage::get_singleton()->material_free(p_rid);
		return true;
	}
	return false;
}

void Utilities::base_update_dependency(RID p_base, DependencyTracker *p_instance) {
	if (RendererDummy::MeshStorage::get_singleton()->owns_mesh(p_base)) {
		DummyMesh *mesh = RendererDummy::MeshStorage::get_singleton()->get_mesh(p_base);
		p_instance->update_dependency(&mesh->dependency);
	}
}

Utilities::Utilities() {
	singleton = this;
}

Utilities::~Utilities() {
	singleton = nullptr;
}
