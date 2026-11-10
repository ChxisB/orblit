/**************************************************************************/
/*  random_number_generator.cpp                                           */

#include "random_number_generator.h"

#include "core/object/class_registry.h"

void RandomNumberGenerator::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_seed", "seed"), &RandomNumberGenerator::set_seed);
	ClassRegistry::bind_method(D_METHOD("get_seed"), &RandomNumberGenerator::get_seed);

	ClassRegistry::bind_method(D_METHOD("set_state", "state"), &RandomNumberGenerator::set_state);
	ClassRegistry::bind_method(D_METHOD("get_state"), &RandomNumberGenerator::get_state);

	ClassRegistry::bind_method(D_METHOD("randi"), &RandomNumberGenerator::randi);
	ClassRegistry::bind_method(D_METHOD("randf"), &RandomNumberGenerator::randf);
	ClassRegistry::bind_method(D_METHOD("randfn", "mean", "deviation"), &RandomNumberGenerator::randfn, DEFVAL(0.0), DEFVAL(1.0));
	ClassRegistry::bind_method(D_METHOD("randf_range", "from", "to"), &RandomNumberGenerator::randf_range);
	ClassRegistry::bind_method(D_METHOD("randi_range", "from", "to"), &RandomNumberGenerator::randi_range);
	ClassRegistry::bind_method(D_METHOD("rand_weighted", "weights"), &RandomNumberGenerator::rand_weighted);
	ClassRegistry::bind_method(D_METHOD("randomize"), &RandomNumberGenerator::randomize);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "seed"), "set_seed", "get_seed");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "state"), "set_state", "get_state");
	// Default values are non-deterministic, override for doc generation purposes.
	ADD_PROPERTY_DEFAULT("seed", 0);
	ADD_PROPERTY_DEFAULT("state", 0);
}
