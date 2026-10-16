package dev.orblit.filament

import android.view.Surface

/**
 * The producer's end of a feed, which Dart makes with `OrblitFeedSource.create`
 * and shows with `OrblitFeed`.
 *
 * A feed is a queue the renderer reads. Whatever draws into [surface] is what
 * the screen shows: a camera session, a decoder, an EGL surface or a hardware
 * canvas. Feeds need API 26. Below it, Dart's `create` fails with the reason
 * and [surface] answers null.
 */
object OrblitFeeds {
    /**
     * A Surface that draws into feed [id], or null if there is no such feed.
     *
     * Each call makes a new Surface over the same queue. The caller releases
     * it. A feed has one reader, so only one screen shows it at a time.
     */
    @JvmStatic
    fun surface(id: Long): Surface? = OrblitNative.nativeFeedSurface(id)
}
