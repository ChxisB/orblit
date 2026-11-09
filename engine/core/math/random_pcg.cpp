/**************************************************************************/
/*  random_pcg.cpp                                                        */

#include "random_pcg.h"

#include "core/containers/vector.h"
#include "core/os/os.h"

RandomPCG::RandomPCG(uint64_t p_seed, uint64_t p_inc) :
		pcg(),
		current_inc(p_inc) {
	seed(p_seed);
}

void RandomPCG::randomize() {
	seed(((uint64_t)OS::get_singleton()->get_unix_time() + OS::get_singleton()->get_ticks_usec()) * pcg.state + PCG_DEFAULT_INC_64);
}

int64_t RandomPCG::rand_weighted(const Vector<float> &p_weights) {
	ERR_FAIL_COND_V_MSG(p_weights.is_empty(), -1, "Weights array is empty.");
	int64_t weights_size = p_weights.size();
	const float *weights = p_weights.ptr();
	float weights_sum = 0.0;
	for (int64_t i = 0; i < weights_size; ++i) {
		ERR_FAIL_COND_V_MSG(weights[i] < 0.0, -1, vformat("Weight array contains the negative value %f at index %d.", weights[i], i));
		weights_sum += weights[i];
	}

	float remaining_distance = randf() * weights_sum;
	for (int64_t i = 0; i < weights_size; ++i) {
		remaining_distance -= weights[i];
		if (remaining_distance < 0) {
			return i;
		}
	}

	for (int64_t i = weights_size - 1; i >= 0; --i) {
		if (weights[i] > 0) {
			return i;
		}
	}
	return -1;
}

double RandomPCG::random(double p_from, double p_to) {
	return randd() * (p_to - p_from) + p_from;
}

float RandomPCG::random(float p_from, float p_to) {
	return randf() * (p_to - p_from) + p_from;
}

int RandomPCG::random(int p_from, int p_to) {
	if (p_from == p_to) {
		return p_from;
	}

	int64_t min = MIN(p_from, p_to);
	int64_t max = MAX(p_from, p_to);
	uint32_t diff = static_cast<uint32_t>(max - min);

	if (diff == UINT32_MAX) {
		// Can't add 1 to max uint32_t value for inclusive range, so call rand without passing bounds.
		return static_cast<int64_t>(rand()) + min;
	}

	return static_cast<int64_t>(rand(diff + 1U)) + min;
}
