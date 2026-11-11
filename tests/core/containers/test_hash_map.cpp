/**************************************************************************/
/*  test_hash_map.cpp                                                     */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_hash_map)

#include "core/containers/hash_map.h"

namespace TestHashMap {

TEST_CASE("[HashMap] List initialization") {
	HashMap<int, String> map{ { 0, "A" }, { 1, "B" }, { 2, "C" }, { 3, "D" }, { 4, "E" } };

	CHECK(map.size() == 5);
	CHECK(map[0] == "A");
	CHECK(map[1] == "B");
	CHECK(map[2] == "C");
	CHECK(map[3] == "D");
	CHECK(map[4] == "E");
}

TEST_CASE("[HashMap] List initialization with existing elements") {
	HashMap<int, String> map{ { 0, "A" }, { 0, "B" }, { 0, "C" }, { 0, "D" }, { 0, "E" } };

	CHECK(map.size() == 1);
	CHECK(map[0] == "E");
}

TEST_CASE("[HashMap] Insert element") {
	HashMap<int, int> map;
	HashMap<int, int>::Iterator e = map.insert(42, 84);

	CHECK(e);
	CHECK(e->key == 42);
	CHECK(e->value == 84);
	CHECK(map[42] == 84);
	CHECK(map.has(42));
	CHECK(map.find(42));
}

TEST_CASE("[HashMap] Overwrite element") {
	HashMap<int, int> map;
	map.insert(42, 84);
	map.insert(42, 1234);

	CHECK(map[42] == 1234);
}

TEST_CASE("[HashMap] Erase via element") {
	HashMap<int, int> map;
	HashMap<int, int>::Iterator e = map.insert(42, 84);
	map.remove(e);
	CHECK(!map.has(42));
	CHECK(!map.find(42));
}

TEST_CASE("[HashMap] Erase via key") {
	HashMap<int, int> map;
	map.insert(42, 84);
	map.erase(42);
	CHECK(!map.has(42));
	CHECK(!map.find(42));
}

TEST_CASE("[HashMap] Replace key") {
	HashMap<int, int> map;
	map.insert(42, 84);
	map.insert(0, 12934);
	CHECK(map.replace_key(0, 1));
	CHECK(!map.has(0));
	CHECK(map[1] == 12934);
}

TEST_CASE("[HashMap] Replace key with a key the map treats as equal") {
	// NaN != NaN, but the map's comparison counts it as one key.
	HashMap<float, int> map;
	map.insert(Math::NaN, 7);
	CHECK(map.replace_key(Math::NaN, Math::NaN));
	CHECK(map.size() == 1);
	CHECK(map[Math::NaN] == 7);
}

TEST_CASE("[HashMap] Size") {
	HashMap<int, int> map;
	map.insert(42, 84);
	map.insert(123, 84);
	map.insert(123, 84);
	map.insert(0, 84);
	map.insert(123485, 84);

	CHECK(map.size() == 4);
}

TEST_CASE("[HashMap] Iteration") {
	HashMap<int, int> map;
	map.insert(42, 84);
	map.insert(123, 12385);
	map.insert(0, 12934);
	map.insert(123485, 1238888);
	map.insert(123, 111111);

	Vector<Pair<int, int>> expected;
	expected.push_back(Pair<int, int>(42, 84));
	expected.push_back(Pair<int, int>(123, 111111));
	expected.push_back(Pair<int, int>(0, 12934));
	expected.push_back(Pair<int, int>(123485, 1238888));

	int idx = 0;
	for (const KeyValue<int, int> &E : map) {
		CHECK(expected[idx] == Pair<int, int>(E.key, E.value));
		++idx;
	}
}

TEST_CASE("[HashMap] Const iteration") {
	HashMap<int, int> map;
	map.insert(42, 84);
	map.insert(123, 12385);
	map.insert(0, 12934);
	map.insert(123485, 1238888);
	map.insert(123, 111111);

	const HashMap<int, int> const_map(map);

	Vector<Pair<int, int>> expected;
	expected.push_back(Pair<int, int>(42, 84));
	expected.push_back(Pair<int, int>(123, 111111));
	expected.push_back(Pair<int, int>(0, 12934));
	expected.push_back(Pair<int, int>(123485, 1238888));
	expected.push_back(Pair<int, int>(123, 111111));

	int idx = 0;
	for (const KeyValue<int, int> &E : const_map) {
		CHECK(expected[idx] == Pair<int, int>(E.key, E.value));
		++idx;
	}
}

TEST_CASE("[HashMap] Sort") {
	HashMap<int, int> hashmap;
	int shuffled_ints[]{ 6, 1, 9, 8, 3, 0, 4, 5, 7, 2 };

	for (int i : shuffled_ints) {
		hashmap[i] = i;
	}
	hashmap.sort();

	int i = 0;
	for (const KeyValue<int, int> &kv : hashmap) {
		CHECK_EQ(kv.key, i);
		i++;
	}

	struct ReverseSort {
		bool operator()(const KeyValue<int, int> &p_a, const KeyValue<int, int> &p_b) {
			return p_a.key > p_b.key;
		}
	};
	hashmap.sort_custom<ReverseSort>();

	for (const KeyValue<int, int> &kv : hashmap) {
		i--;
		CHECK_EQ(kv.key, i);
	}
}

} // namespace TestHashMap
