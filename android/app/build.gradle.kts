plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.kapt")
    id("org.jetbrains.kotlin.plugin.compose")
    id("org.jetbrains.kotlin.plugin.serialization")
}

android {
    namespace = "com.teamxv.qrmonitor"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.xv.accesscontrol"
        minSdk = 26
        targetSdk = 35
        versionCode = 100
        versionName = "1.0.0"
        ndk {
            abiFilters += listOf("arm64-v8a", "armeabi-v7a")
            // CI runs the on-device tests on an x86_64 emulator (-PxvEmulator); phones use the ARM libraries only.
            if (project.hasProperty("xvEmulator")) abiFilters += "x86_64"
        }
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"

    }

    // Release signing key from the environment (CI secrets); falls back to the build machine's debug key.
    val releaseKeystore = System.getenv("XV_KEYSTORE_FILE")?.let { file(it) }?.takeIf { it.exists() }
    signingConfigs {
        if (releaseKeystore != null) create("release") {
            storeFile = releaseKeystore
            storePassword = System.getenv("XV_KEYSTORE_PASSWORD")
            keyAlias = System.getenv("XV_KEY_ALIAS")
            keyPassword = System.getenv("XV_KEY_PASSWORD")
        }
    }

    buildTypes {
        debug {
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
            manifestPlaceholders["allowCleartextTraffic"] = false
        }
        release {
            manifestPlaceholders["allowCleartextTraffic"] = false
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            // Your own release key when XV_KEYSTORE_FILE is set (stable signature: updates install over the old app);
            // otherwise the build machine's debug key so the APK still installs directly.
            signingConfig = if (releaseKeystore != null) signingConfigs.getByName("release") else signingConfigs.getByName("debug")
        }
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    sourceSets.getByName("androidTest").assets.srcDir("$projectDir/schemas")
    // Call page shared with the Windows Command Center (served from assets/call/call.html)
    sourceSets.getByName("main").assets.srcDir("$projectDir/../../shared")

    packaging {
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2024.12.01")
    implementation(composeBom)
    androidTestImplementation(composeBom)

    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.activity:activity-compose:1.10.0")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.8.7")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.7")
    implementation("androidx.navigation:navigation-compose:2.8.5")

    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")
    debugImplementation("androidx.compose.ui:ui-tooling")

    implementation("androidx.room:room-runtime:2.8.5")
    implementation("androidx.room:room-ktx:2.8.5")
    kapt("androidx.room:room-compiler:2.8.5")
    testImplementation("androidx.room:room-testing:2.8.5")

    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.7.3")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("androidx.webkit:webkit:1.12.1")
    // Encrypted local database (SQLCipher for Android)
    implementation("net.zetetic:sqlcipher-android:4.6.1@aar")
    implementation("androidx.sqlite:sqlite:2.4.0")

    implementation("androidx.work:work-runtime-ktx:2.11.2")

    implementation("androidx.camera:camera-core:1.4.2")
    implementation("androidx.camera:camera-camera2:1.4.2")
    implementation("androidx.camera:camera-lifecycle:1.4.2")
    implementation("androidx.camera:camera-view:1.4.2")
    implementation("androidx.camera:camera-mlkit-vision:1.4.2")
    implementation("com.google.mlkit:barcode-scanning:17.3.0")

    testImplementation("junit:junit:4.13.2")
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
    androidTestImplementation("androidx.test:runner:1.6.2")
    androidTestImplementation("androidx.test:core-ktx:1.6.1")
    androidTestImplementation("androidx.test.espresso:espresso-core:3.6.1")
    androidTestImplementation("androidx.compose.ui:ui-test-junit4")
    debugImplementation("androidx.compose.ui:ui-test-manifest")
    androidTestImplementation("androidx.work:work-testing:2.11.2")
}


kapt {
    arguments {
        arg("room.schemaLocation", "$projectDir/schemas")
        arg("room.incremental", "true")
    }
}
