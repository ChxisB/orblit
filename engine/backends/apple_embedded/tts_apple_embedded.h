/**************************************************************************/
/*  tts_apple_embedded.h                                                  */

#pragma once

#include "core/string/ustring.h"
#include "core/containers/hash_map.h"
#include "core/containers/list.h"
#include "core/variant/array.h"

#if __has_include(<AVFAudio/AVSpeechSynthesis.h>)
#import <AVFAudio/AVSpeechSynthesis.h>
#else
#import <AVFoundation/AVFoundation.h>
#endif

struct TTSUtterance;

@interface GDTTTS : NSObject <AVSpeechSynthesizerDelegate> {
	bool speaking;
	HashMap<id, int64_t> ids;

	AVSpeechSynthesizer *av_synth;
	List<TTSUtterance> queue;
}

- (void)pauseSpeaking;
- (void)resumeSpeaking;
- (void)stopSpeaking;
- (bool)isSpeaking;
- (bool)isPaused;
- (void)speak:(const String &)text voice:(const String &)voice volume:(int)volume pitch:(float)pitch rate:(float)rate utterance_id:(int64_t)utterance_id interrupt:(bool)interrupt;
- (Array)getVoices;
@end
