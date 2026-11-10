/**************************************************************************/
/*  ogg_packet_sequence.h                                                 */

#pragma once

#include "core/io/resource.h"
#include "core/variant/typed_array.h"
#include "core/variant/variant.h"

#include <ogg/ogg.h>

class OggPacketSequencePlayback;

class OggPacketSequence : public Resource {
	ORBLIT_CLASS(OggPacketSequence, Resource);

	friend class OggPacketSequencePlayback;

	// List of pages, each of which is a list of packets on that page. The innermost PackedByteArrays contain complete ogg packets.
	Vector<Vector<PackedByteArray>> page_data;

	// List of the granule position for each page.
	Vector<uint64_t> page_granule_positions;

	// The page after the current last page. Similar semantics to an end() iterator.
	int64_t end_page = 0;

	uint64_t data_version = 0;

	float sampling_rate = 0;
	float length = 0;

protected:
	static void register_members();

public:
	// Pushes information about all the pages that ended on this page.
	// This should be called for each page, even for pages that no packets ended on.
	void push_page(int64_t p_granule_pos, const Vector<PackedByteArray> &p_data);

	void set_packet_data(const TypedArray<Array> &p_data);
	TypedArray<Array> get_packet_data() const;

	void set_packet_granule_positions(const PackedInt64Array &p_granule_positions);
	PackedInt64Array get_packet_granule_positions() const;

	// Sets a sampling rate associated with this object. OggPacketSequence doesn't understand codecs,
	// so this value is naively stored as a convenience.
	void set_sampling_rate(float p_sampling_rate);

	// Returns a sampling rate previously set by set_sampling_rate().
	float get_sampling_rate() const;

	// Returns a length previously set by set_length().
	float get_length() const;

	// Returns the granule position of the last page in this sequence.
	int64_t get_final_granule_pos() const;

	Ref<OggPacketSequencePlayback> instantiate_playback();

	OggPacketSequence() {}
	virtual ~OggPacketSequence() {}
};

class OggPacketSequencePlayback : public RefCounted {
	ORBLIT_CLASS(OggPacketSequencePlayback, RefCounted);

	friend class OggPacketSequence;

	Ref<OggPacketSequence> ogg_packet_sequence;

	mutable int64_t page_cursor = 0;
	mutable int32_t packet_cursor = 0;

	mutable ogg_packet *packet;

	uint64_t data_version = 0;

	mutable int64_t packetno = 0;

	// Recursive bisection search for the correct page.
	uint32_t seek_page_internal(int64_t granule, uint32_t after_page_inclusive, uint32_t before_page_inclusive);

public:
	// Calling functions must not modify this packet.
	// Returns true on success, false on error or if there is no next packet.
	bool next_ogg_packet(ogg_packet **p_packet) const;

	// Seeks to the page such that the previous page has a granule position less than or equal to this value,
	// and the current page has a granule position greater than this value.
	// Returns true on success, false on failure.
	bool seek_page(int64_t p_granule_pos);

	// Gets the current page number.
	int64_t get_page_number() const;

	// Moves to a specific page in the stream.
	// Returns true on success, false if the page number is out of bounds.
	bool set_page_number(int64_t p_page_number);

	OggPacketSequencePlayback();
	virtual ~OggPacketSequencePlayback();
};
