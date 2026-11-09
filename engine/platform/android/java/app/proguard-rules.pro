# Protect Orblit Engine Core
-keep class com.orblit.** { *; }
-keep class org.orblitengine.** { *; }
-keep class ** extends org.orblitengine.orblit.plugin.OrblitPlugin { *; }

# Protect methods exposed to Orblit via @UsedByOrblit
-keepattributes *Annotation*
-keepclassmembers class * {
    @org.orblitengine.orblit.plugin.UsedByOrblit *;
}

# Protect JNI bindings
-keepclasseswithmembernames class * {
    native <methods>;
}
-keep public class * extends android.app.Activity
-keep public class * extends android.app.Application
-keep public class * extends android.app.Service
