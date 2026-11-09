/**************************************************************************/
/*  test_self_list.cpp                                                    */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_self_list)

#include "core/containers/self_list.h"

namespace TestSelfList {

TEST_CASE("[SelfList] Sort") {
	const int SIZE = 5;
	int numbers[SIZE]{ 3, 2, 5, 1, 4 };
	SelfList<int> elements[SIZE]{
		SelfList<int>(&numbers[0]),
		SelfList<int>(&numbers[1]),
		SelfList<int>(&numbers[2]),
		SelfList<int>(&numbers[3]),
		SelfList<int>(&numbers[4]),
	};

	SelfList<int>::List list;
	for (int i = 0; i < SIZE; i++) {
		list.add_last(&elements[i]);
	}

	SelfList<int> *it = list.first();
	for (int i = 0; i < SIZE; i++) {
		CHECK_EQ(numbers[i], *it->self());
		it = it->next();
	}

	list.sort();
	it = list.first();
	for (int i = 1; i <= SIZE; i++) {
		CHECK_EQ(i, *it->self());
		it = it->next();
	}

	for (SelfList<int> &element : elements) {
		element.remove_from_list();
	}
}

} // namespace TestSelfList
