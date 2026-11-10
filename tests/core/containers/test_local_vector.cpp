/**************************************************************************/
/*  test_local_vector.cpp                                                 */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_local_vector)

#include "core/containers/local_vector.h"

namespace TestLocalVector {

TEST_CASE("[LocalVector] List Initialization.") {
	LocalVector<int> vector{ 0, 1, 2, 3, 4 };

	CHECK(vector.size() == 5);
	CHECK(vector[0] == 0);
	CHECK(vector[1] == 1);
	CHECK(vector[2] == 2);
	CHECK(vector[3] == 3);
	CHECK(vector[4] == 4);
}

TEST_CASE("[LocalVector] Push Back.") {
	LocalVector<int> vector;
	vector.push_back(0);
	vector.push_back(1);
	vector.push_back(2);
	vector.push_back(3);
	vector.push_back(4);

	CHECK(vector[0] == 0);
	CHECK(vector[1] == 1);
	CHECK(vector[2] == 2);
	CHECK(vector[3] == 3);
	CHECK(vector[4] == 4);
}

TEST_CASE("[LocalVector] Find, has.") {
	LocalVector<int> vector;
	vector.push_back(3);
	vector.push_back(1);
	vector.push_back(4);
	vector.push_back(0);
	vector.push_back(2);

	CHECK(vector[0] == 3);
	CHECK(vector[1] == 1);
	CHECK(vector[2] == 4);
	CHECK(vector[3] == 0);
	CHECK(vector[4] == 2);

	CHECK(vector.find(0) == 3);
	CHECK(vector.find(1) == 1);
	CHECK(vector.find(2) == 4);
	CHECK(vector.find(3) == 0);
	CHECK(vector.find(4) == 2);

	CHECK(vector.find(-1) == -1);
	CHECK(vector.find(5) == -1);

	CHECK(vector.has(0));
	CHECK(vector.has(1));
	CHECK(vector.has(2));
	CHECK(vector.has(3));
	CHECK(vector.has(4));

	CHECK(!vector.has(-1));
	CHECK(!vector.has(5));
}

TEST_CASE("[LocalVector] Remove.") {
	LocalVector<int> vector;
	vector.push_back(0);
	vector.push_back(1);
	vector.push_back(2);
	vector.push_back(3);
	vector.push_back(4);

	vector.remove_at(0);

	CHECK(vector[0] == 1);
	CHECK(vector[1] == 2);
	CHECK(vector[2] == 3);
	CHECK(vector[3] == 4);

	vector.remove_at(2);

	CHECK(vector[0] == 1);
	CHECK(vector[1] == 2);
	CHECK(vector[2] == 4);

	vector.remove_at(1);

	CHECK(vector[0] == 1);
	CHECK(vector[1] == 4);

	vector.remove_at(0);

	CHECK(vector[0] == 4);
}

TEST_CASE("[LocalVector] Remove Unordered.") {
	LocalVector<int> vector;
	vector.push_back(0);
	vector.push_back(1);
	vector.push_back(2);
	vector.push_back(3);
	vector.push_back(4);

	CHECK(vector.size() == 5);

	vector.remove_at_unordered(0);

	CHECK(vector.size() == 4);

	CHECK(vector.find(0) == -1);
	CHECK(vector.find(1) != -1);
	CHECK(vector.find(2) != -1);
	CHECK(vector.find(3) != -1);
	CHECK(vector.find(4) != -1);

	// Now the vector is no more ordered.
	vector.remove_at_unordered(vector.find(3));

	CHECK(vector.size() == 3);

	CHECK(vector.find(3) == -1);
	CHECK(vector.find(1) != -1);
	CHECK(vector.find(2) != -1);
	CHECK(vector.find(4) != -1);

	vector.remove_at_unordered(vector.find(2));

	CHECK(vector.size() == 2);

	CHECK(vector.find(2) == -1);
	CHECK(vector.find(1) != -1);
	CHECK(vector.find(4) != -1);

	vector.remove_at_unordered(vector.find(4));

	CHECK(vector.size() == 1);

	CHECK(vector.find(4) == -1);
	CHECK(vector.find(1) != -1);

	// Remove the last one.
	vector.remove_at_unordered(0);

	CHECK(vector.is_empty());
	CHECK(vector.size() == 0);
}

TEST_CASE("[LocalVector] Erase Unordered.") {
	LocalVector<int> vector;
	vector.push_back(1);
	vector.push_back(3);
	vector.push_back(0);
	vector.push_back(2);
	vector.push_back(4);

	CHECK(vector.find(1) == 0);

	vector.erase_unordered(1);

	CHECK(vector.find(1) == -1);
	CHECK(vector.size() == 4);
	CHECK(vector[0] == 4);
}

TEST_CASE("[LocalVector] Erase.") {
	LocalVector<int> vector;
	vector.push_back(1);
	vector.push_back(3);
	vector.push_back(0);
	vector.push_back(2);
	vector.push_back(4);

	CHECK(vector.find(2) == 3);

	vector.erase(2);

	CHECK(vector.find(2) == -1);
	CHECK(vector.size() == 4);
}

TEST_CASE("[LocalVector] Size / Resize / Reserve.") {
	LocalVector<int> vector;

	CHECK(vector.is_empty());
	CHECK(vector.size() == 0);
	CHECK(vector.get_capacity() == 0);

	vector.resize(10);

	CHECK(vector.size() == 10);
	CHECK(vector.get_capacity() >= 10);

	vector.resize(5);

	CHECK(vector.size() == 5);
	// Capacity is supposed to change only when the size increase.
	CHECK(vector.get_capacity() >= 10);

	vector.remove_at(0);
	vector.remove_at(0);
	vector.remove_at(0);

	CHECK(vector.size() == 2);
	// Capacity is supposed to change only when the size increase.
	CHECK(vector.get_capacity() >= 10);

	vector.reset();

	CHECK(vector.size() == 0);
	CHECK(vector.get_capacity() == 0);

	vector.reserve(3);

	CHECK(vector.is_empty());
	CHECK(vector.size() == 0);
	CHECK(vector.get_capacity() >= 3);

	vector.push_back(0);
	vector.push_back(0);
	vector.push_back(0);

	CHECK(vector.size() == 3);
	CHECK(vector.get_capacity() >= 3);

	vector.push_back(0);

	CHECK(vector.size() == 4);
	CHECK(vector.get_capacity() >= 4);
}

} // namespace TestLocalVector
