/**************************************************************************/
/*  OrblitUtterance.java                                                  */

package org.orblitengine.orblit.tts;

/**
 * A speech request for OrblitTTS.
 */
class OrblitUtterance {
	final String text;
	final String voice;
	final int volume;
	final float pitch;
	final float rate;
	final long id;

	int offset = -1;
	int start = 0;

	OrblitUtterance(String text, String voice, int volume, float pitch, float rate, long id) {
		this.text = text;
		this.voice = voice;
		this.volume = volume;
		this.pitch = pitch;
		this.rate = rate;
		this.id = id;
	}
}
