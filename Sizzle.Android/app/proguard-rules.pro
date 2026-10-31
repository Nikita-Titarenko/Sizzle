# kotlinx.serialization: keep generated serializers of the API models.
-keepattributes *Annotation*, InnerClasses, Signature, Exceptions
-keepclassmembers @kotlinx.serialization.Serializable class com.sizzle.app.** {
    *** Companion;
    kotlinx.serialization.KSerializer serializer(...);
}
-keep,includedescriptorclasses class com.sizzle.app.**$$serializer { *; }

# Retrofit interfaces are used through reflection.
-keep,allowobfuscation interface com.sizzle.app.data.api.SizzleApi
-keep,allowobfuscation,allowshrinking class kotlin.coroutines.Continuation
-keep,allowobfuscation,allowshrinking class retrofit2.Response
