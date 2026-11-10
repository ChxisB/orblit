/**************************************************************************/
/*  test_span.cpp                                                         */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_span)

#include "core/containers/span.h"

namespace TestSpan {

TEST_CASE("[Span] Constexpr Validators") {
	constexpr Span<uint16_t> span_empty;
	static_assert(span_empty.ptr() == nullptr);
	static_assert(span_empty.size() == 0);
	static_assert(span_empty.is_empty());

	constexpr static uint16_t value = 5;
	Span<uint16_t> span_value(&value, 1);
	CHECK(span_value.ptr() == &value);
	CHECK(span_value.size() == 1);
	CHECK(!span_value.is_empty());

	static constexpr int ints[] = { 0, 1, 2, 3, 4, 5 };
	constexpr Span<int> span_array = ints;
	static_assert(span_array.size() == 6);
	static_assert(!span_array.is_empty());
	static_assert(span_array[0] == 0);
	static_assert(span_array[span_array.size() - 1] == 5);

	constexpr Span<char32_t> span_string = U"122345";
	static_assert(span_string.size() == 6);
	static_assert(!span_string.is_empty());
	static_assert(span_string[0] == U'1');
	static_assert(span_string[span_string.size() - 1] == U'5');

	CHECK_EQ(span_string, span_string); // Same identity / ptr.
	CHECK_EQ(span_string, Span(U"1223456", 6)); // Different ptr.
	CHECK_EQ(span_string, Span("122345").reinterpret<uint8_t>()); // Different type.

	int idx = 0;
	for (const char32_t &chr : span_string) {
		CHECK_EQ(chr, span_string[idx++]);
	}
}

} // namespace TestSpan
