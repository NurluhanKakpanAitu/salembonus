import type { CapacitorConfig } from '@capacitor/cli'

// App Store мен Google Play-ге арналған нативті қабық (STORE.md). Веб-бөлігі — сол PWA,
// тек `npm run build:native` арқылы жиналады: толық API мекенжайы, service worker-сіз.
const config: CapacitorConfig = {
  // Идентификатор дүкенге бір рет тіркеледі, кейін өзгертуге болмайды.
  appId: 'kz.salembonus.app',
  appName: 'SalemBonus',
  webDir: 'dist',
  // Android-та бет https://localhost-тан ашылады, iOS-та capacitor://localhost-тан —
  // екеуі де бэкендтің Cors:AppOrigins тізімінде.
  android: {
    allowMixedContent: false,
    // Release билдте WebView-ды Chrome арқылы ашуға болмайды.
    webContentsDebuggingEnabled: false,
  },
  ios: {
    // Қауіпсіз аймақтарды бет өзі env(safe-area-inset-*) арқылы ескереді.
    contentInset: 'never',
    scheme: 'capacitor',
  },
  plugins: {
    SplashScreen: {
      // Бет жүктелгенде native.ts өзі жасырады — ақ жыпылықтау болмайды.
      launchAutoHide: false,
      backgroundColor: '#F4F5F8',
      androidScaleType: 'CENTER_CROP',
      showSpinner: false,
    },
    PushNotifications: {
      // iOS: қосымша ашық тұрғанда да push баннері көрінеді (Android-та бет тізімді өзі жаңартады).
      presentationOptions: ['badge', 'sound', 'alert'],
    },
    SystemBars: {
      // Android 15+ (edge-to-edge): қауіпсіз аймақтар CSS айнымалылары ретінде беріледі.
      insetsHandling: 'css',
    },
  },
}

export default config
