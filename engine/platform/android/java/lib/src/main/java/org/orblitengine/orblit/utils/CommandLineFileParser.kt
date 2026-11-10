/**************************************************************************/
/*  CommandLineFileParser.kt                                              */

package org.orblitengine.orblit.utils

import java.io.InputStream
import java.nio.charset.StandardCharsets
import java.util.ArrayList

/**
 * A class that parses the content of file storing command line params. Usually, this file is saved
 * in `assets/_cl_` on exporting an apk
 *
 * Returns a mutable list of command lines
 */
internal object CommandLineFileParser {
	fun parseCommandLine(inputStream: InputStream): MutableList<String> {
		return try {
			val headerBytes = ByteArray(4)
			var argBytes = inputStream.read(headerBytes)
			if (argBytes < 4) {
				return mutableListOf()
			}
			val argc = decodeHeaderIntValue(headerBytes)

			val cmdline = ArrayList<String>(argc)
			for (i in 0 until argc) {
				argBytes = inputStream.read(headerBytes)
				if (argBytes < 4) {
					return mutableListOf()
				}
				val strlen = decodeHeaderIntValue(headerBytes)

				if (strlen > 65535) {
					return mutableListOf()
				}

				val arg = ByteArray(strlen)
				argBytes = inputStream.read(arg)
				if (argBytes == strlen) {
					cmdline.add(String(arg, StandardCharsets.UTF_8))
				}
			}
			cmdline
		} catch (e: Exception) {
			// The _cl_ file can be missing with no adverse effect
			mutableListOf()
		}
	}

	private fun decodeHeaderIntValue(headerBytes: ByteArray): Int =
		(headerBytes[3].toInt() and 0xFF) shl 24 or
		((headerBytes[2].toInt() and 0xFF) shl 16) or
		((headerBytes[1].toInt() and 0xFF) shl 8) or
		(headerBytes[0].toInt() and 0xFF)
}
