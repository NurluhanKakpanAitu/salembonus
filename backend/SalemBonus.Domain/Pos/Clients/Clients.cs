namespace SalemBonus.Domain.Pos.Clients;

public enum ClientGender { Unknown, Male, Female }
public enum ClientSource { Manual, Cashier, Import, App, Integration }

/// <summary>
/// Ұйымның клиент карточкасы (ТЗ «Клиенты» §5, §23). Жаһандық <c>Customer</c> — адамның SalemBonus-тағы аккаунты (телефон — бірегей),
/// ал бұл — бизнестің CRM жазбасы: дүкен білетін аты-жөні, жынысы, мекенжайы, келісімдері, қосымша өрістері, мұрағат белгісі.
/// Сатылым, чек, қарыз, төлем мен бонус өз модульдерінде — карточка оларды CustomerId арқылы байланыстырады, көшірмесін жасамайды.
/// </summary>
public class ClientProfile
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CustomerId { get; set; }
    /// <summary>Ұйымдағы реттік нөмір («ID: 000125»).</summary>
    public long Number { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public ClientGender Gender { get; set; }
    /// <summary>Облыс/аудан/елді мекен — КАТО коды (ең төменгі таңдалғаны).</summary>
    public string? KatoCode { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public ClientSource Source { get; set; }
    public string? Comment { get; set; }
    /// <summary>Әр арнаға жеке келісім (§5): әдепкіде өшірулі, автоматты қосылмайды.</summary>
    public bool ConsentApp { get; set; }
    public bool ConsentSms { get; set; }
    public bool ConsentWhatsApp { get; set; }
    public bool ConsentEmail { get; set; }
    public DateTime? ConsentUpdatedAt { get; set; }
    /// <summary>Баптауларда анықталған қосымша өрістердің мәндері (кілт → мән).</summary>
    public Dictionary<string, string> CustomFields { get; set; } = [];
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedBy { get; set; }
    public string? ArchiveReason { get; set; }
    /// <summary>Біріктірілген болса — негізгі карточка (бұл карточка мұрағатта қалады).</summary>
    public Guid? MergedIntoId { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
}

public enum NoteCategory { General, Preference, Complaint, Call, Other }

/// <summary>Қызметкер жазбасы (§13): автор мен күн автоматты; өзгерту — автор не рұқсаты бар рөл, аудитпен.</summary>
public class ClientNote
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProfileId { get; set; }
    public string Text { get; set; } = string.Empty;
    public NoteCategory Category { get; set; }
    public Guid AuthorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>Сүзгі шарттары (§4.3) — сегмент те, кесте де осыны қолданады.</summary>
public class ClientFilter
{
    /// <summary>Жылдам сүзгі: active, passive, new, repeat, debt, overdue, bonus, birthday, noPurchases, archived.</summary>
    public string? Quick { get; set; }
    public string? Search { get; set; }
    public List<Guid> StatusIds { get; set; } = [];
    public DateOnly? RegisteredFrom { get; set; }
    public DateOnly? RegisteredTo { get; set; }
    public DateOnly? LastPurchaseFrom { get; set; }
    public DateOnly? LastPurchaseTo { get; set; }
    public int? MinPurchases { get; set; }
    public int? MaxPurchases { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal? MinAverage { get; set; }
    public decimal? MaxAverage { get; set; }
    public int? MinBonus { get; set; }
    public int? MaxBonus { get; set; }
    public bool? HasDebt { get; set; }
    public bool? Overdue { get; set; }
    /// <summary>КАТО коды не оның басы (облыс → аудан → елді мекен).</summary>
    public string? Kato { get; set; }
    /// <summary>Male, Female, Unknown.</summary>
    public string? Gender { get; set; }
    public int? BirthMonth { get; set; }
    /// <summary>Manual, Cashier, Import, App, Integration.</summary>
    public string? Source { get; set; }
    /// <summary>Келісім арнасы: app, sms, whatsapp, email.</summary>
    public string? Consent { get; set; }
}

/// <summary>Динамикалық сегмент (§16): шарттар сақталады, клиенттер ашылғанда қайта есептеледі; қолмен қосылғандар — бөлек тізім.</summary>
public class ClientSegment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ClientFilter Filter { get; set; } = new();
    public List<Guid> IncludeIds { get; set; } = [];
    public bool IsShared { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CustomFieldDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>text, number, date, select.</summary>
    public string Type { get; set; } = "text";
    public List<string> Options { get; set; } = [];
}

/// <summary>Ұйымның «Клиенты» баптаулары (§21): міндетті өрістер, белсенділік шектері, «скоро срок», қосымша өрістер, бағандар.</summary>
public class ClientSettings
{
    public Guid OrganizationId { get; set; }
    public bool RequireLastName { get; set; } = true;
    public bool RequireBirthDate { get; set; }
    public int ActiveDays { get; set; } = 30;
    public int PassiveDays { get; set; } = 90;
    public int NewDays { get; set; } = 30;
    public int DueSoonDays { get; set; } = 3;
    public List<string> DefaultColumns { get; set; } = [];
    public List<CustomFieldDefinition> CustomFields { get; set; } = [];
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
}

/// <summary>Клиентке хабар (§15): арна, мәтін, нәтиже — белсенділікке жазылады. Интеграциясыз жіберу имитацияланбайды.</summary>
public class ClientCommunication
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid StoreId { get; set; }
    public string Channel { get; set; } = "app";
    public string Text { get; set; } = string.Empty;
    public string Status { get; set; } = "delivered";
    public Guid? NotificationId { get; set; }
    public Guid StaffUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Біріктіру журналы (§20): кім, қашан, қай карточкалар, не көшірілді.</summary>
public class ClientMergeLog
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid PrimaryProfileId { get; set; }
    public Guid DuplicateProfileId { get; set; }
    public Guid PrimaryCustomerId { get; set; }
    public Guid DuplicateCustomerId { get; set; }
    public string Summary { get; set; } = "{}";
    public Guid StaffUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Импорт (§19.1): кілт бойынша қайталанбайды — сол кілтпен қайта жіберілсе, сақталған нәтиже қайтарылады.</summary>
public class ClientImport
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int Rows { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public string Report { get; set; } = "[]";
    public Guid StaffUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
