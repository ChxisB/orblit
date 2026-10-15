package dev.orblit.filament

import android.content.res.AssetFileDescriptor
import android.content.res.AssetManager
import android.util.Log
import java.io.File
import java.io.FileNotFoundException

/**
 * Where an application asset sits as a file, for a reader that wants a path
 * and a range rather than bytes in memory.
 *
 * Android keeps assets inside the APK, which is a file the process can open.
 * An asset stored without compression is one contiguous run of that file, and
 * [AssetManager.openFd] says where. The descriptor's `/proc/self/fd` entry
 * reopens the APK itself, so a reader seeks to [Range.offset] and reads
 * [Range.length] bytes, and a pack of hundreds of megabytes is never held in
 * memory. The build has to leave the asset uncompressed; the app's
 * `noCompress` list is where.
 *
 * A compressed asset cannot be opened that way. It is copied to the cache
 * folder once per process instead and the range is the whole copy. That works
 * and it is slow and large, so it is logged as a mistake in the build.
 */
internal class AssetFiles(
    private val assets: AssetManager,
    private val cache: File,
) {
    class Range(val path: String, val offset: Long, val length: Long)

    private val open = mutableMapOf<String, AssetFileDescriptor>()
    private val copied = mutableMapOf<String, Range>()

    /** The range of [asset], opened by its path inside the APK. */
    @Synchronized
    fun range(asset: String): Range {
        copied[asset]?.let { return it }
        open[asset]?.let { return rangeOf(it) }
        val descriptor = try {
            assets.openFd(asset)
        } catch (e: FileNotFoundException) {
            // openFd gives this for a missing asset and a compressed one
            // alike. The copy opens a stream first, which a missing asset
            // refuses with the same exception.
            return copy(asset)
        }
        open[asset] = descriptor
        return rangeOf(descriptor)
    }

    /** Lets go of [asset]'s descriptor or copy, and says whether it held one. */
    @Synchronized
    fun release(asset: String): Boolean {
        val descriptor = open.remove(asset)
        descriptor?.close()
        return (copied.remove(asset) != null) || descriptor != null
    }

    @Synchronized
    fun close() {
        for (descriptor in open.values) descriptor.close()
        open.clear()
        copied.clear()
    }

    private fun rangeOf(descriptor: AssetFileDescriptor): Range =
        Range(
            path = "/proc/self/fd/${descriptor.parcelFileDescriptor.fd}",
            offset = descriptor.startOffset,
            length = descriptor.length,
        )

    private fun copy(asset: String): Range {
        // Opened first, so a missing asset leaves nothing behind.
        assets.open(asset).use { input ->
            Log.w(
                "OrblitFilament",
                "$asset is compressed in the APK, so it is copied to the cache " +
                    "to be read. Add its extension to noCompress.",
            )
            val folder = File(cache, "orblit-assets").apply { mkdirs() }
            val target = File(folder, asset.replace('/', '_'))
            // Written beside the target and renamed, so a copy that stopped
            // halfway is never what a reader opens.
            val part = File(folder, target.name + ".part")
            part.outputStream().use { input.copyTo(it) }
            if (!part.renameTo(target)) {
                part.delete()
                throw FileNotFoundException("could not place $asset in the cache")
            }
            val range = Range(target.absolutePath, 0, target.length())
            copied[asset] = range
            return range
        }
    }
}
