namespace SalemBonus.Infrastructure.Push;

/// <summary>
/// Push кілттері — тек конфигурациядан (user-secrets / орта айнымалылары Push__...), кодта ешқашан жоқ.
/// Платформаның кілті жоқ болса, оған ештеңе жіберілмейді (STORE.md 7-бөлім).
/// </summary>
public class PushOptions
{
    public const string Section = "Push";

    public FcmOptions Fcm { get; set; } = new();
    public ApnsOptions Apns { get; set; } = new();
}

/// <summary>Android: Firebase Cloud Messaging HTTP v1, Firebase жобасының service account кілті.</summary>
public class FcmOptions
{
    /// <summary>Service account JSON файлының жолы (Firebase → Project settings → Service accounts → Generate new private key).</summary>
    public string ServiceAccountFile { get; set; } = string.Empty;
    /// <summary>Не JSON-ның өзі (файл орнына, мысалы орта айнымалысы ретінде).</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    public bool IsConfigured => ServiceAccountFile.Length > 0 || ServiceAccountJson.Length > 0;
}

/// <summary>iOS: APNs-ке тікелей, токен негізіндегі кілт (.p8). Firebase SDK iOS қосымшасына керек емес.</summary>
public class ApnsOptions
{
    /// <summary>Кілттің Key ID-і (developer.apple.com → Keys).</summary>
    public string KeyId { get; set; } = string.Empty;
    /// <summary>Apple Developer командасының Team ID-і.</summary>
    public string TeamId { get; set; } = string.Empty;
    public string BundleId { get; set; } = "kz.salembonus.app";
    /// <summary>.p8 файлының жолы.</summary>
    public string PrivateKeyFile { get; set; } = string.Empty;
    /// <summary>Не .p8 мазмұны (-----BEGIN PRIVATE KEY----- ...).</summary>
    public string PrivateKey { get; set; } = string.Empty;
    /// <summary>
    /// Xcode-тан тікелей орнатылған билд (Debug) sandbox токенін береді; TestFlight пен App Store — production.
    /// Продта false.
    /// </summary>
    public bool Sandbox { get; set; }

    public bool IsConfigured => KeyId.Length > 0 && TeamId.Length > 0 && BundleId.Length > 0
        && (PrivateKeyFile.Length > 0 || PrivateKey.Length > 0);
}
