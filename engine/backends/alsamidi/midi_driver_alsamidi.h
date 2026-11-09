/**************************************************************************/
/*  midi_driver_alsamidi.h                                                */

#pragma once

#ifdef ALSAMIDI_ENABLED

#include "core/containers/safe_refcount.h"
#include "core/containers/vector.h"
#include "core/os/midi_driver.h"
#include "core/os/mutex.h"
#include "core/os/thread.h"

typedef struct _snd_rawmidi snd_rawmidi_t;

class MIDIDriverALSAMidi : public MIDIDriver {
	Thread thread;
	Mutex mutex;

	struct InputConnection {
		InputConnection() = default;
		InputConnection(int p_device_index, snd_rawmidi_t *p_rawmidi);

		Parser parser;
		snd_rawmidi_t *rawmidi_ptr = nullptr;

		// Read in and parse available data, forwarding complete events to Input.
		void read();
	};

	Vector<InputConnection> connected_inputs;

	SafeFlag exit_thread;

	static void thread_func(void *p_udata);

	void lock() const;
	void unlock() const;

public:
	virtual Error open() override;
	virtual void close() override;

	MIDIDriverALSAMidi();
	virtual ~MIDIDriverALSAMidi();
};

#endif // ALSAMIDI_ENABLED
