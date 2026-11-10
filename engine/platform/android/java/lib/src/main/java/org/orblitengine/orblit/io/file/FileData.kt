/**************************************************************************/
/*  FileData.kt                                                           */

package org.orblitengine.orblit.io.file

import android.os.*
import java.io.File
import java.io.FileOutputStream
import java.io.RandomAccessFile
import java.nio.channels.FileChannel
import java.nio.file.attribute.BasicFileAttributes
import java.nio.file.Files
import java.nio.file.FileSystems
import java.nio.file.Path
import java.util.concurrent.TimeUnit

/**
 * Implementation of [DataAccess] which handles regular (not scoped) file access and interactions.
 */
internal class FileData(filePath: String, accessFlag: FileAccessFlags) : DataAccess.FileChannelDataAccess(filePath) {

	companion object {
		private val TAG = FileData::class.java.simpleName

		fun fileExists(path: String): Boolean {
			return try {
				File(path).isFile
			} catch (e: SecurityException) {
				false
			}
		}

		fun fileLastModified(filepath: String): Long {
			return try {
				File(filepath).lastModified() / 1000L
			} catch (e: SecurityException) {
				0L
			}
		}

		fun fileLastAccessed(filepath: String): Long {
			return try {
				if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
					Files.readAttributes<BasicFileAttributes>(FileSystems.getDefault().getPath(filepath), BasicFileAttributes::class.java).lastAccessTime().to(TimeUnit.SECONDS)
				} else {
					0L
				}
			} catch (e: SecurityException) {
				0L
			}
		}

		fun fileSize(filepath: String): Long {
			return try {
				if (File(filepath).isFile) {
					File(filepath).length()
				} else {
					-1L
				}
			} catch (e: SecurityException) {
				-1L
			}
		}

		fun delete(filepath: String): Boolean {
			return try {
				File(filepath).delete()
			} catch (e: Exception) {
				false
			}
		}

		fun rename(from: String, to: String): Boolean {
			return try {
				val fromFile = File(from)
				fromFile.renameTo(File(to))
			} catch (e: Exception) {
				false
			}
		}
	}

	override val fileChannel: FileChannel

	init {
		fileChannel = if (accessFlag == FileAccessFlags.WRITE) {
			// Create parent directory is necessary
			val parentDir = File(filePath).parentFile
			if (parentDir != null && !parentDir.exists()) {
				parentDir.mkdirs()
			}

			FileOutputStream(filePath, !accessFlag.shouldTruncate()).channel
		} else {
			RandomAccessFile(filePath, accessFlag.getMode()).channel
		}

		if (accessFlag.shouldTruncate()) {
			fileChannel.truncate(0)
		}
	}
}
