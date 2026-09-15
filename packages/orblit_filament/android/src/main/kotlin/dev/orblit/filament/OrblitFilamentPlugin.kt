package dev.orblit.filament

import android.util.Log
import io.flutter.embedding.engine.plugins.FlutterPlugin
import io.flutter.plugin.common.MethodCall
import io.flutter.plugin.common.MethodChannel
import io.flutter.plugin.common.MethodChannel.MethodCallHandler
import io.flutter.plugin.common.MethodChannel.Result
import io.flutter.plugin.common.StandardMethodCodec
import io.flutter.view.TextureRegistry

/**
 * Android's `orblit_filament` plugin: the same channel, the same methods, the
 * same wire shapes as OrblitFilamentPlugin.swift, so `lib/src/orblit_view.dart`
 * and every other Dart caller work unchanged. One [OrblitViewport] per
 * `create`d texture, keyed by the id Flutter's texture registry gave it --
 * `viewports` here is exactly `Viewport`'s dictionary on the Swift side.
 */
class OrblitFilamentPlugin :
    FlutterPlugin,
    MethodCallHandler {

    private lateinit var channel: MethodChannel
    private lateinit var resources: MethodChannel
    private var textureRegistry: TextureRegistry? = null
    private val viewports = mutableMapOf<Long, OrblitViewport>()

    override fun onAttachedToEngine(binding: FlutterPlugin.FlutterPluginBinding) {
        textureRegistry = binding.textureRegistry
        channel = MethodChannel(binding.binaryMessenger, "orblit_filament")
        channel.setMethodCallHandler(this)

        // Bytes by name, off the platform thread: a large model decoded out
        // of a message is work the interface should not wait behind.
        resources = MethodChannel(
            binding.binaryMessenger,
            "orblit_filament/resources",
            StandardMethodCodec.INSTANCE,
            binding.binaryMessenger.makeBackgroundTaskQueue(),
        )
        resources.setMethodCallHandler { call, result -> onResourceCall(call, result) }
    }

    /** `provide` and `release`: see orblit_renderer_provide_resource. */
    private fun onResourceCall(call: MethodCall, result: Result) {
        val args = call.arguments as? Map<*, *>
        val name = args?.get("name") as? String
        if (name.isNullOrEmpty()) {
            result.error("bad-args", "${call.method} needs a name", null)
            return
        }
        try {
            when (call.method) {
                "provide" -> {
                    val bytes = args?.get("bytes") as? ByteArray
                    if (bytes == null) {
                        result.error("bad-args", "provide needs bytes", null)
                        return
                    }
                    val status = OrblitNative.nativeProvideResource(name, bytes)
                    if (status == 0) {
                        result.success(null)
                    } else {
                        result.error("orblit_failed", "the renderer could not keep $name ($status)", null)
                    }
                }

                "release" -> result.success(OrblitNative.nativeReleaseResource(name) == 0)

                else -> result.notImplemented()
            }
        } catch (e: Throwable) {
            Log.e("OrblitFilament", "resource ${call.method} failed", e)
            result.error("orblit_failed", "${e.javaClass.simpleName}: ${e.message}", null)
        }
    }

    override fun onMethodCall(call: MethodCall, result: Result) {
        try {
            when (call.method) {
                "create" -> {
                    val args = call.arguments as? Map<*, *>
                    val width = (args?.get("width") as? Number)?.toInt()
                    val height = (args?.get("height") as? Number)?.toInt()
                    if (width == null || height == null) {
                        result.error("bad-args", "create needs width and height", null)
                        return
                    }
                    val registry = textureRegistry
                    if (registry == null) {
                        result.error("no-registry", "No TextureRegistry", null)
                        return
                    }
                    val viewport = OrblitViewport(registry)
                    val textureId =
                        try {
                            viewport.start(width, height)
                        } catch (e: IllegalStateException) {
                            result.error("no-renderer", e.message, null)
                            return
                        }
                    viewports[textureId] = viewport
                    result.success(textureId)
                }

                "resize" -> {
                    val args = call.arguments as? Map<*, *>
                    val textureId = (args?.get("textureId") as? Number)?.toLong()
                    val width = (args?.get("width") as? Number)?.toInt()
                    val height = (args?.get("height") as? Number)?.toInt()
                    if (textureId == null || width == null || height == null) {
                        result.error("bad-args", "resize needs textureId, width, height", null)
                        return
                    }
                    viewports[textureId]?.resize(width, height)
                    result.success(null)
                }

                "setScene" -> {
                    @Suppress("UNCHECKED_CAST")
                    val args = call.arguments as? Map<String, Any?>
                    val textureId = (args?.get("textureId") as? Number)?.toLong()
                    if (args == null || textureId == null) {
                        result.error("bad-args", "setScene needs a textureId", null)
                        return
                    }
                    val scene = OrblitScene.from(args)
                    if (scene == null) {
                        result.error(
                            "bad-scene",
                            "setScene needs keys, float32 transforms (16 each), colours " +
                                "(3 each), flags, lights (16 floats each), fog (10 floats) " +
                                "and a camera.",
                            null,
                        )
                        return
                    }
                    val viewport = viewports[textureId]
                    if (viewport == null) {
                        result.success(null)
                        return
                    }
                    // Answered once the scene is actually in, as the Swift
                    // plugin's does -- there is no separate engine thread on
                    // this side to make that a real asynchronous hop yet
                    // (see OrblitViewport's class comment), but the shape is
                    // kept the same so that stops being true without this
                    // call site changing.
                    viewport.applyScene(scene) { notes -> result.success(notes) }
                }

                "stats" -> {
                    val args = call.arguments as? Map<*, *>
                    val textureId = (args?.get("textureId") as? Number)?.toLong()
                    val viewport = textureId?.let { viewports[it] }
                    if (viewport == null) {
                        result.success(null)
                        return
                    }
                    result.success(viewport.stats())
                }

                "capabilities" -> {
                    val args = call.arguments as? Map<*, *>
                    val textureId = (args?.get("textureId") as? Number)?.toLong()
                    result.success(textureId?.let { viewports[it] }?.capabilities())
                }

                "dispose" -> {
                    val args = call.arguments as? Map<*, *>
                    val textureId = (args?.get("textureId") as? Number)?.toLong()
                    if (textureId == null) {
                        result.error("bad-args", "dispose needs textureId", null)
                        return
                    }
                    viewports.remove(textureId)?.dispose()
                    result.success(null)
                }

                else -> result.notImplemented()
            }
        } catch (e: Throwable) {
            // Including Error, not just Exception: an UnsatisfiedLinkError
            // from a mis-mangled JNI name or a missing .so is the single
            // most likely failure on a first Android build, and it is far
            // more useful shown in the app's own error text than buried in
            // logcat -- the android-spike's plugin does the same for the
            // same reason.
            Log.e("OrblitFilament", "method ${call.method} failed", e)
            result.error("orblit_failed", "${e.javaClass.simpleName}: ${e.message}", null)
        }
    }

    override fun onDetachedFromEngine(binding: FlutterPlugin.FlutterPluginBinding) {
        for (viewport in viewports.values) viewport.dispose()
        viewports.clear()
        channel.setMethodCallHandler(null)
        resources.setMethodCallHandler(null)
        textureRegistry = null
    }
}
