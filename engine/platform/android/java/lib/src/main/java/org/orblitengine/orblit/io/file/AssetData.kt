/**************************************************************************/
/*  AssetData.kt                                                          */

package org.orblitengine.orblit.io.file

import android.content.Context
import android.content.res.AssetManager
import android.util.Log
import org.orblitengine.orblit.error.Error
import org.orblitengine.orblit.io.directory.AssetsDirectoryAccess
import java.io.IOException
import java.io.InputStream
import java.lang.UnsupportedOperationException
import java.nio.ByteBuffer
import java.nio.channels.Channels
import java.nio.channels.ReadableByteChannel

/**
 * Implementation of the [DataAccess] which handles access and interaction with files in the
 * 'assets' directory
 */
internal class AssetData(context: Context, private val filePath: String, accessFlag: FileAccessFlags) : DataAccess() {

	companion object {
		private val TAG = AssetData::class.java.simpleName

		fun fileExists(context: Context, path: String): Boolean {
			val assetsPath = AssetsDirectoryAccess.getAssetsPath(path)
			try {
				val files = context.assets.list(assetsPath) ?: return false
				// Empty directories don't get added to the 'assets' directory, so
				// if files.length > 0 ==> path is directory
				// if files.length == 0 ==> path is file
				return files.isEmpty()
			} catch (e: IOException) {
				Log.e(TAG, "Exception on fileExists", e)
				return false
			}
		}

		fun fileLastModified(path: String) = 0L
		fun fileLastAccessed(path: String) = 0L
		fun fileSize(path: String) = -1L

		fun delete(path: String) = false

		fun rename(from: String, to: String) = false
	}

	private val inputStream: InputStream
	internal val readChannel: ReadableByteChannel

	private var position = 0L
	private val length: Long

	init {
		if (accessFlag == FileAccessFlags.WRITE) {
			throw UnsupportedOperationException("Writing to the 'assets' directory is not supported")
		}

		val assetsPath = AssetsDirectoryAccess.getAssetsPath(filePath)
		inputStream = context.assets.open(assetsPath, AssetManager.ACCESS_BUFFER)
		readChannel = Channels.newChannel(inputStream)

		length = inputStream.available().toLong()
	}

	override fun close() {
		try {
			inputStream.close()
		} catch (e: IOException) {
			Log.w(TAG, "Exception when closing file $filePath.", e)
		}
	}

	override fun flush() {
		Log.w(TAG, "flush() is not supported.")
	}

	override fun seek(position: Long) {
		try {
			inputStream.reset();
			inputStream.skip(position)

			this.position = position
			if (this.position > length) {
				this.position = length
				endOfFile = true
			} else {
				endOfFile = false
			}

		} catch(e: IOException) {
			Log.w(TAG, "Exception when seeking file $filePath.", e)
		}
	}

	override fun resize(length: Long): Error {
		Log.w(TAG, "resize() is not supported.")
		return Error.ERR_UNAVAILABLE
	}

	override fun position() = position

	override fun size() = length

	override fun read(buffer: ByteBuffer): Int {
		return try {
			val readBytes = readChannel.read(buffer)
			if (readBytes == -1) {
				endOfFile = true
				0
			} else {
				position += readBytes
				readBytes
			}
		} catch (e: IOException) {
			Log.w(TAG, "Exception while reading from $filePath.", e)
			0
		}
	}

	override fun write(buffer: ByteBuffer): Boolean {
		Log.w(TAG, "write() is not supported.")
		return false
	}
}
