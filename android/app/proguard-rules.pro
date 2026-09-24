# Keep the app's own classes (serialized payloads, Room entities, workers).
-keep class com.teamxv.qrmonitor.** { *; }
# SQLCipher native bridge is called from JNI.
-keep class net.zetetic.** { *; }
-keepclassmembers class * { native <methods>; }
# kotlinx.serialization
-keepattributes *Annotation*, InnerClasses, Signature, EnclosingMethod
-dontnote kotlinx.serialization.**
-keepclassmembers class kotlinx.serialization.json.** { *** Companion; }
# OkHttp optional platform classes
-dontwarn okhttp3.internal.platform.**
-dontwarn org.conscrypt.**
-dontwarn org.bouncycastle.**
-dontwarn org.openjsse.**
# Call page JavaScript bridge (WebView) must keep its method names in release builds.
-keepclassmembers class * { @android.webkit.JavascriptInterface <methods>; }
