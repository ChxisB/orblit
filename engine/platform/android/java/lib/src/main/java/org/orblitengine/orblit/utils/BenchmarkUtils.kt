/**************************************************************************/
/*  BenchmarkUtils.kt                                                     */

@file:JvmName("BenchmarkUtils")

package org.orblitengine.orblit.utils

import android.os.Build
import android.os.SystemClock
import android.os.Trace
import android.util.Log
import org.orblitengine.orblit.BuildConfig
import org.orblitengine.orblit.error.Error
import org.orblitengine.orblit.io.file.FileAccessFlags
import org.orblitengine.orblit.io.file.FileAccessHandler
import org.json.JSONObject
import java.nio.ByteBuffer
import java.util.Collections

/**
 * Contains benchmark related utilities methods
 */
private const val TAG = "OrblitBenchmark"

var useBenchmark = false
var benchmarkFile = ""

private val startBenchmarkFrom = Collections.synchronizedMap(LinkedHashMap<Pair<String, String>, Long>())
private val benchmarkTracker = Collections.synchronizedMap(LinkedHashMap<Pair<String, String>, Double>())

/**
 * Start measuring and tracing the execution of a given section of code using the given label
 * within the given scope.
 *
 * Must be followed by a call to [endBenchmarkMeasure].
 *
 * Note: Only enabled on 'editorDev' build variant.
 */
fun beginBenchmarkMeasure(scope: String, label: String) {
	if (BuildConfig.FLAVOR != "editor") {
		return
	}
	val key = Pair(scope, label)
	startBenchmarkFrom[key] = SystemClock.elapsedRealtime()

	if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
		Trace.beginAsyncSection("[$scope] $label", 0)
	}
}

/**
 * End measuring and tracing of the section of code with the given label within the given scope.
 *
 * Must be preceded by a call [beginBenchmarkMeasure]
 *
 * * Note: Only enabled on 'editorDev' build variant.
 */
@JvmOverloads
fun endBenchmarkMeasure(scope: String, label: String, dumpBenchmark: Boolean = false) {
	if (BuildConfig.FLAVOR != "editor") {
		return
	}
	val key = Pair(scope, label)
	val startTime = startBenchmarkFrom[key] ?: return
	val total = SystemClock.elapsedRealtime() - startTime
	benchmarkTracker[key] = total / 1_000.0

	if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
		Trace.endAsyncSection("[$scope] $label", 0)
	}

	if (dumpBenchmark) {
		dumpBenchmark()
	}
}

/**
 * Dump the benchmark measurements.
 * If [filepath] is valid, the data is also written in json format to the specified file.
 *
 * * Note: Only enabled on 'editorDev' build variant.
 */
@JvmOverloads
fun dumpBenchmark(fileAccessHandler: FileAccessHandler? = null, filepath: String? = benchmarkFile) {
	if (BuildConfig.FLAVOR != "editor") {
		return
	}
	if (!useBenchmark || benchmarkTracker.isEmpty()) {
		return
	}

	val results = LinkedHashMap<String, String>()
	for (entry in benchmarkTracker) {
		if (!results.containsKey(entry.key.first)) {
			results[entry.key.first] = ""
		}
		results[entry.key.first] += "\t\t- ${entry.key.second}: ${entry.value * 1_000.0} msec.\n"
	}

	val printOut =
		results.map { "\t- [${it.key}]\n ${it.value}" }.joinToString("\n")
	Log.i(TAG, "BENCHMARK:\n$printOut")

	if (fileAccessHandler != null && !filepath.isNullOrBlank()) {
		val (fileError, fileId) = fileAccessHandler.fileOpen(filepath, FileAccessFlags.WRITE)
		if (fileError == Error.OK) {
			val jsonOutput = JSONObject(benchmarkTracker.toMap()).toString(4)
			fileAccessHandler.fileWrite(fileId, ByteBuffer.wrap(jsonOutput.toByteArray()))
			fileAccessHandler.fileClose(fileId)
		}
	}
}
