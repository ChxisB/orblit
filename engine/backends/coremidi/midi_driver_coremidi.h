/**************************************************************************/
/*  midi_driver_coremidi.h                                                */

#pragma once

#ifdef COREMIDI_ENABLED

#include "core/os/midi_driver.h"
#include "core/os/mutex.h"
#include "core/containers/vector.h"

#import <CoreMIDI/CoreMIDI.h>

#include <cstdio>

class MIDIDriverCoreMidi : public MIDIDriver {
	MIDIClientRef client = 0;
	MIDIPortRef port_in;

	struct InputConnection {
		InputConnection() = default;
		InputConnection(int p_device_index, MIDIEndpointRef p_source);
		Parser parser;
		MIDIEndpointRef source;
	};

	Vector<InputConnection *> connected_sources;

	static Mutex mutex;
	static bool core_midi_closed;

	static void read(const MIDIPacketList *packet_list, void *read_proc_ref_con, void *src_conn_ref_con);

public:
	virtual Error open() override;
	virtual void close() override;

	MIDIDriverCoreMidi() = default;
	virtual ~MIDIDriverCoreMidi();
};

#endif // COREMIDI_ENABLED
