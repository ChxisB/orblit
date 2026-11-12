/**************************************************************************/
/*  collision_object_spatial.cpp                                               */

#include "collision_object_spatial.h"

#include "physics_system_spatial.h"
#include "space_spatial.h"

void OrblitCollisionObject3D::add_shape(OrblitShape3D *p_shape, const Transform3D &p_transform, bool p_disabled) {
	Shape s;
	s.shape = p_shape;
	s.xform = p_transform;
	s.xform_inv = s.xform.affine_inverse();
	s.bpid = 0; //needs update
	s.disabled = p_disabled;
	shapes.push_back(s);
	p_shape->add_owner(this);

	if (!pending_shape_update_list.in_list()) {
		OrblitPhysicsSystem3D::orblit_singleton->pending_shape_update_list.add(&pending_shape_update_list);
	}
}

void OrblitCollisionObject3D::set_shape(int p_index, OrblitShape3D *p_shape) {
	ERR_FAIL_INDEX(p_index, shapes.size());
	shapes[p_index].shape->remove_owner(this);
	shapes.write[p_index].shape = p_shape;

	p_shape->add_owner(this);
	if (!pending_shape_update_list.in_list()) {
		OrblitPhysicsSystem3D::orblit_singleton->pending_shape_update_list.add(&pending_shape_update_list);
	}
}

void OrblitCollisionObject3D::set_shape_transform(int p_index, const Transform3D &p_transform) {
	ERR_FAIL_INDEX(p_index, shapes.size());

	shapes.write[p_index].xform = p_transform;
	shapes.write[p_index].xform_inv = p_transform.affine_inverse();
	if (!pending_shape_update_list.in_list()) {
		OrblitPhysicsSystem3D::orblit_singleton->pending_shape_update_list.add(&pending_shape_update_list);
	}
}

void OrblitCollisionObject3D::set_shape_disabled(int p_idx, bool p_disabled) {
	ERR_FAIL_INDEX(p_idx, shapes.size());

	OrblitCollisionObject3D::Shape &shape = shapes.write[p_idx];
	if (shape.disabled == p_disabled) {
		return;
	}

	shape.disabled = p_disabled;

	if (!space) {
		return;
	}

	if (p_disabled && shape.bpid != 0) {
		space->get_broadphase()->remove(shape.bpid);
		shape.bpid = 0;
		if (!pending_shape_update_list.in_list()) {
			OrblitPhysicsSystem3D::orblit_singleton->pending_shape_update_list.add(&pending_shape_update_list);
		}
	} else if (!p_disabled && shape.bpid == 0) {
		if (!pending_shape_update_list.in_list()) {
			OrblitPhysicsSystem3D::orblit_singleton->pending_shape_update_list.add(&pending_shape_update_list);
		}
	}
}

void OrblitCollisionObject3D::remove_shape(OrblitShape3D *p_shape) {
	//remove a shape, all the times it appears
	for (int i = 0; i < shapes.size(); i++) {
		if (shapes[i].shape == p_shape) {
			remove_shape(i);
			i--;
		}
	}
}

void OrblitCollisionObject3D::remove_shape(int p_index) {
	//remove anything from shape to be erased to end, so subindices don't change
	ERR_FAIL_INDEX(p_index, shapes.size());
	for (int i = p_index; i < shapes.size(); i++) {
		if (shapes[i].bpid == 0) {
			continue;
		}
		//should never get here with a null owner
		space->get_broadphase()->remove(shapes[i].bpid);
		shapes.write[i].bpid = 0;
	}
	shapes[p_index].shape->remove_owner(this);
	shapes.remove_at(p_index);

	if (!pending_shape_update_list.in_list()) {
		OrblitPhysicsSystem3D::orblit_singleton->pending_shape_update_list.add(&pending_shape_update_list);
	}
}

void OrblitCollisionObject3D::_set_static(bool p_static) {
	if (_static == p_static) {
		return;
	}
	_static = p_static;

	if (!space) {
		return;
	}
	for (int i = 0; i < get_shape_count(); i++) {
		const Shape &s = shapes[i];
		if (s.bpid > 0) {
			space->get_broadphase()->set_static(s.bpid, _static);
		}
	}
}

void OrblitCollisionObject3D::_unregister_shapes() {
	for (int i = 0; i < shapes.size(); i++) {
		Shape &s = shapes.write[i];
		if (s.bpid > 0) {
			space->get_broadphase()->remove(s.bpid);
			s.bpid = 0;
		}
	}
}

AABB OrblitCollisionObject3D::_get_shape_broadphase_aabb(const Shape &p_shape) const {
	AABB aabb = (transform * p_shape.xform).xform(p_shape.shape->get_aabb());
	// The margin comes from the box itself. Taking it from the cached box grew the result on each update.
	aabb.grow_by((aabb.size.x + aabb.size.y) * 0.5 * 0.05);
	return aabb;
}

void OrblitCollisionObject3D::_update_shapes() {
	if (!space) {
		return;
	}

	for (int i = 0; i < shapes.size(); i++) {
		Shape &s = shapes.write[i];
		if (s.disabled) {
			continue;
		}

		//not quite correct, should compute the next matrix..
		const AABB shape_aabb = _get_shape_broadphase_aabb(s);
		s.aabb_cache = shape_aabb;

		Vector3 scale = (transform * s.xform).get_basis().get_scale();
		s.area_cache = s.shape->get_volume() * scale.x * scale.y * scale.z;

		if (s.bpid == 0) {
			s.bpid = space->get_broadphase()->create(this, i, shape_aabb, _static);
			space->get_broadphase()->set_static(s.bpid, _static);
		}

		space->get_broadphase()->move(s.bpid, shape_aabb);
	}
}

void OrblitCollisionObject3D::_update_shapes_with_motion(const Vector3 &p_motion) {
	if (!space) {
		return;
	}

	for (int i = 0; i < shapes.size(); i++) {
		Shape &s = shapes.write[i];
		if (s.disabled) {
			continue;
		}

		//not quite correct, should compute the next matrix..
		AABB shape_aabb = _get_shape_broadphase_aabb(s);
		shape_aabb.merge_with(AABB(shape_aabb.position + p_motion, shape_aabb.size)); //use motion
		s.aabb_cache = shape_aabb;

		if (s.bpid == 0) {
			s.bpid = space->get_broadphase()->create(this, i, shape_aabb, _static);
			space->get_broadphase()->set_static(s.bpid, _static);
		}

		space->get_broadphase()->move(s.bpid, shape_aabb);
	}
}

void OrblitCollisionObject3D::_set_space(OrblitSpace3D *p_space) {
	OrblitSpace3D *old_space = space;
	space = p_space;

	if (old_space) {
		old_space->remove_object(this);

		for (int i = 0; i < shapes.size(); i++) {
			Shape &s = shapes.write[i];
			if (s.bpid) {
				old_space->get_broadphase()->remove(s.bpid);
				s.bpid = 0;
			}
		}
	}

	if (space) {
		space->add_object(this);
		_update_shapes();
	}
}

void OrblitCollisionObject3D::_shape_changed() {
	_update_shapes();
	_shapes_changed();
}

OrblitCollisionObject3D::OrblitCollisionObject3D(Type p_type) :
		pending_shape_update_list(this) {
	type = p_type;
}
