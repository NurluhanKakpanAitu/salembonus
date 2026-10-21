# SalemBonus: App Store мен Google Play-ге шығару — иесінің тізімі

Код жағы дайын: `frontend/android` пен `frontend/ios` — Capacitor 8 жобалары, иконкалар, splash, рұқсат
мәтіндері, дүкен суреттері мен мәтіндері осы құжатта. Төменде **тек иесі** істей алатын қадамдар, ретімен.
Неге Capacitor және жалпы жоспар — [MOBILE.md](MOBILE.md).

| | Android | iOS |
|---|---|---|
| Идентификатор | `kz.salembonus.app` (applicationId) | `kz.salembonus.app` (Bundle ID) |
| Атауы | SalemBonus | SalemBonus |
| Нұсқа | `1.0.0`, versionCode `1` | `1.0.0`, build `1` |
| Жүйе | Android 7.0+ (minSdk 24), target 36 | iOS 15+, тек iPhone, тек тік экран |
| Билд қайда | Windows не Mac (Android Studio) | **тек Mac** (Xcode 26) |

> Идентификатор дүкенге бір рет тіркеледі, кейін өзгермейді.

---

## 0. Код жағында не жасалды

- `frontend/capacitor.config.ts`, `frontend/android/`, `frontend/ios/` — нативті жобалар (Swift Package Manager, CocoaPods керек емес).
- `npm run build:native` — `.env.native`-тағы толық API мекенжайымен (`https://api.salemtech.kz/api`), service worker-сіз жинайды;
  `npm run cap:sync` — жинап, екі жобаға көшіреді.
- Бэкенд CORS-ы `capacitor://localhost` (iOS) пен `https://localhost` (Android) мекенжайларын өзі жібереді (`Cors:AppOrigins`,
  `appsettings.json`) — серверде ештеңе қосудың қажеті жоқ. Кіру — Bearer токенмен, cookie керек емес.
- Қауіпсіз аймақтар (notch, төменгі жолақ), Android «артқа» батырмасы (алдымен ашық терезені жабады, сосын артқа, басты бетте — шығу),
  интернет жоқ кезде жоғарғы жолақ, тақырыпқа сай жүйелік жолақ түсі.
- Нативті мүмкіндіктер (Apple 4.2 үшін): камерамен дүкен QR кодын сканерлеу, QR көрсеткенде экран жарықтығы 100%-ға шығады,
  сканер сәтті болғанда діріл (haptics), нативті splash.
- Аккаунтты өшіру қосымшаның ішінде бұрыннан бар (Профиль → Аккаунтты өшіру). Енді SalemPos-та сатып алуы не қарызы бар клиент
  те өшіріледі: чек дүкенде клиентсіз қалады; өтелмеген қарыз болса, қосымша қай дүкенде қарыз барын айтып, өшірмейді.
- Тексерушіге арналған нөмір: `REVIEW_PHONE` / `REVIEW_CODE` — тек осы нөмірге тұрақты код, SMS жіберілмейді.
- `frontend/store/` — дүкен суреттері: иконка 1024 және 512, Play feature graphic 1024×500, скриншоттар (App Store 6.9″
  1320×2868 және Google Play 1080×1920, орысша мен қазақша жазумен). Қайта жасау: `npm run native:assets`.
- iOS: камера мен фото рұқсатының мәтіні (орысша / қазақша), `PrivacyInfo.xcprivacy`, `ITSAppUsesNonExemptEncryption = NO`
  (экспорттық шифрлау сұрағы қайта қойылмайды).

---

## 1. Дүкенге жібермей тұрып — серверде

1. **SMS-шлюз.** Продта `STATIC_OTP_CODE` тұрғанда кез келген адам кез келген нөмірге кіреді — тексеруші мұны байқаса,
   қауіпсіздік бойынша қайтарады. SMS провайдерін қосып, `.env`-тен `STATIC_OTP_CODE`-ты алып таста.
2. **Тексерушінің нөмірі.** `/opt/salembonus/.env` (не Traefik-тегі compose) ішіне:
   ```
   REVIEW_PHONE=+77000000000
   REVIEW_CODE=<кездейсоқ 4 сан>
   ```
   Нөмір нақты адамдікі болмасын. Сосын API-ді қайта қос: `docker compose -f docker-compose.prod.yml up -d api`.
3. **Демо деректер.** Сол нөмірмен қосымшаға кір, профильді толтыр. SalemPos-та (pos.salemtech.kz) сол клиентке бір-екі
   сатылым жаса — тексеруші картаны, балансты, операцияларды және хабарламаны көрсін. Дүкеннің QR коды плакатын не
   кодын да тексерушіге бер (2.4 және 3.4-тегі «Notes»).
4. `support@salembonus.kz` пошта жәшігі жұмыс істейтінін тексер — дүкен беттерінде көрсетіледі.
5. Құпиялылық саясатын заңгерге көрсет ([MOBILE.md](MOBILE.md) 1.2).

---

## 2. Google Play

### 2.1. Есептік жазба — $25 бір рет
https://play.google.com/console/signup → **Organization** (D-U-N-S нөмірімен). Жеке есептік жазба болса, жарияламас бұрын
12 тестермен 14 күн жабық тест міндетті.

### 2.2. Қолтаңба кілті (бір рет, жоғалтпа)

Android Studio орнат (ішінде JDK бар). Кілт — **upload key**; дүкендегі соңғы қолтаңбаны Google сақтайды (Play App Signing).

```bash
keytool -genkeypair -v -keystore salembonus-upload.jks -alias salembonus -keyalg RSA -keysize 2048 -validity 10000
```

`frontend/android/key.properties` жаса (git-ке кірмейді, `.gitignore`-да бар):

```
storeFile=C:/кілттер/salembonus-upload.jks
storePassword=...
keyAlias=salembonus
keyPassword=...
```

`.jks` файлы мен парольдерді екі бөлек жерде сақта (мысалы, пароль менеджері + шифрланған диск). Жоғалса — Play Console
арқылы upload key-ді ауыстыруға болады, бірақ бірнеше күн кетеді.

### 2.3. Билд

```bash
cd frontend
npm ci
npm run cap:sync
cd android
./gradlew bundleRelease          # Windows: gradlew.bat bundleRelease
```

Нәтиже: `frontend/android/app/build/outputs/bundle/release/app-release.aab`. Тексеру үшін телефонға: Android Studio →
**Open** `frontend/android` → ▶ Run (USB арқылы қосылған телефон).

### 2.4. Play Console толтыру

**Create app**: аты `SalemBonus`, әдепкі тіл — орысша (ru-RU), App, Free. Сосын **Dashboard → Set up your app**:

| Бөлім | Жауап |
|---|---|
| Privacy policy | `https://salembonus.kz/privacy` |
| App access | All or some functionality is restricted → нөмір `REVIEW_PHONE`, код `REVIEW_CODE`, нұсқау: «Введите номер, нажмите «Код алу», введите код. SMS не отправляется.» |
| Ads | No |
| Content rating | IARC сауалнамасы: Category **Utility/Productivity/Communication/Other**, зорлық/қорқыныш/ойын/сатып алу — бәріне **No** → нәтиже 3+ / Everyone |
| Target audience | **18 and over** (балаларға арналмаған — Families саясаты қолданылмайды) |
| News app | No |
| Data safety | төмендегі кесте |
| Government app | No |
| Financial features | No (бонус — дүкеннің жеңілдігі, қаржы қызметі емес) |
| Health | No |
| App category | **Shopping**; контакт: support@salembonus.kz, сайт https://salembonus.kz |

**Data safety:**

- Does your app collect or share user data? **Yes**. Is all data encrypted in transit? **Yes**.
  Account creation: **phone number + OTP**. Delete account URL: `https://salembonus.kz/delete-account`.
- Shared with third parties: **No** (клиент өзі қосқан дүкен оның картасын көреді — бұл пайдаланушының өз әрекеті, Google
  мұны sharing деп санамайды).

| Түрі (Play) | Жиналады | Міндетті ме | Мақсаты |
|---|---|---|---|
| Personal info → Phone number | Yes | Required | App functionality, Account management |
| Personal info → Name | Yes | Optional | App functionality, Account management |
| Personal info → Email address | Yes | Optional | Account management |
| Personal info → Address (тек облыс/елді мекен) | Yes | Optional | App functionality |
| Personal info → Other info (туған күні) | Yes | Optional | App functionality |
| Personal info → User IDs | Yes | Required | App functionality |
| Financial info → Purchase history | Yes | Required | App functionality |
| Photos and videos → Photos (профиль суреті) | Yes | Optional | App functionality |

Камера кадрлары құрылғыдан шықпайды — оларды «жиналады» деп белгілеме. Орналасу, контактілер, жарнама ID — жиналмайды.

**Main store listing** — 5-бөлімдегі мәтіндер (ru-RU негізгі, kk қосымша аударма ретінде: **Translations → Add → Kazakh**).
Суреттер: `frontend/store/icon-512.png`, `frontend/store/feature-graphic-1024x500.png`,
`frontend/store/screenshots/google-play/ru/*.png` (қазақша бетке — `kk/`).

### 2.5. Жіберу
**Testing → Internal testing** арқылы алдымен өз телефоныңа орнат → тексер → **Production → Create new release** →
`.aab` жүкте → Release notes: «Первая версия» → **Send for review** (1–7 күн).

---

## 3. App Store

### 3.1. Есептік жазба — $99/жыл
https://developer.apple.com/programs/enroll/ → **Organization** (D-U-N-S). Тіркелу 1–3 апта.

### 3.2. Bundle ID
developer.apple.com → Certificates, IDs & Profiles → **Identifiers** → `+` → App IDs → App →
Bundle ID **Explicit** `kz.salembonus.app`, Capabilities — әзірге ешқайсысы (push кейін).
Сертификат пен профильді қолмен жасаудың қажеті жоқ — Xcode автоматты қолтаңбамен өзі жасайды.

### 3.3. Билд (Mac)

Xcode 26 орнат (App Store, тегін). Сосын:

```bash
git clone https://github.com/NurluhanKakpanAitu/salembonus.git && cd salembonus/frontend
npm ci
npm run cap:sync
npx cap open ios
```

Xcode-та:
1. Сол жақта **App** жобасы → **Signing & Capabilities** → **Automatically manage signing** ✓ → **Team** — ұйымның командасы.
   Bundle Identifier `kz.salembonus.app` болуы керек.
2. Жоғарыда құрылғы ретінде **Any iOS Device (arm64)** таңда.
3. **Product → Archive** → Organizer ашылады → **Distribute App** → **App Store Connect** → Upload.
4. Келесі жаңартуда **General → Build** санын (1 → 2 …) және қажет болса **Version** көтер.

Симуляторда тексеру: құрылғы ретінде iPhone 17 таңдап ▶. Камера симуляторда жоқ — сканер «кодты қолмен енгізу»-ге ауысады.

### 3.4. App Store Connect

https://appstoreconnect.apple.com → **Apps → +** → New App: iOS, аты `SalemBonus`, тілі **Russian**, Bundle ID
`kz.salembonus.app`, SKU `salembonus-ios`, Full Access.

| Бөлім | Жауап |
|---|---|
| App Information → Category | Primary **Shopping**, Secondary **Lifestyle** |
| App Information → Content Rights | Үшінші тараптың мазмұны жоқ (дүкен атаулары мен суреттерін дүкендер өзі береді — «Yes, has rights» деп растау) |
| Age Rating | сауалнаманың бәріне **None / No** → **4+** |
| Pricing | Free, барлық ел (не тек Kazakhstan) |
| App Privacy → Privacy Policy URL | `https://salembonus.kz/privacy` |
| App Privacy → Data types | төмендегі кесте |
| Localizations | Russian (негізгі) + **Kazakh** қос — 5-бөлімдегі мәтіндер |
| Screenshots (iPhone 6.9″) | `frontend/store/screenshots/app-store-6.9/ru/*.png` (қазақша — `kk/`) |
| Support URL | `https://salembonus.kz` |
| Copyright | `2026 <заңды тұлғаның атауы>` |
| App Review Information | Sign-in required ✓, User name `REVIEW_PHONE`, Password `REVIEW_CODE`, байланыс телефоны мен поштасы |

**App Privacy** (Data Used to Track You — **жоқ**; бәрі Linked to You, мақсаты **App Functionality**):

| Санат | Түрі |
|---|---|
| Contact Info | Name, Email Address, Phone Number |
| Location | Coarse Location (пайдаланушы өзі таңдаған облыс/елді мекен) |
| User Content | Photos or Videos (профиль суреті, міндетті емес) |
| Identifiers | User ID |
| Purchases | Purchase History |
| Other Data | Other Data Types (туған күні) |

Бұл тізім `frontend/ios/App/App/PrivacyInfo.xcprivacy`-мен бірдей — бірін өзгертсең, екіншісін де.

**Notes for Review** (ағылшынша жаз — тексерушілер көбіне ағылшынша оқиды):

> SalemBonus is a loyalty wallet: one account keeps the bonus cards of many partner stores in Kazakhstan.
> Sign in: enter the phone number above, tap the button, enter the code above (no SMS is sent to this number).
> Native features: camera QR scanner to add a store's bonus card (Stores tab → the blue scan button at the top; store code for manual entry: <КОД>),
> automatic full screen brightness while the customer's QR code is shown at the checkout, haptic feedback,
> account deletion inside the app (Profile → Delete account).

### 3.5. Жіберу
Билд Connect-ке түскен соң (15–30 мин) → **TestFlight** → өзіңді Internal Tester ретінде қосып, телефонда тексер →
App Store бетіне қайт → **Build** таңда → **Add for Review** → **Submit** (1–7 күн).

Ең ықтимал қайтару — **4.2 Minimum Functionality**. Онда Resolution Center-де қай мүмкіндік жетіспейтінін сұра; дайын
жоспар — push-хабарлама (бонус есептелгенде), MOBILE.md 1.4.

---

## 4. Рұқсат мәтіндері

| Қайда | Мәтін |
|---|---|
| iOS `NSCameraUsageDescription` | Камера нужна, чтобы сканировать QR-код магазина и подключить его бонусную карту. / Дүкеннің QR кодын сканерлеп, бонус картасын қосу үшін камера керек. |
| iOS `NSPhotoLibraryUsageDescription` | Доступ к фото нужен, чтобы выбрать фотографию профиля. / Профиль суретін таңдау үшін фотоларға рұқсат керек. |
| Android `CAMERA` | Жүйенің өз терезесі, мәтін керек емес; камерасыз құрылғыда да орнатылады (`required=false`). |
| Хабарламалар (push қосылғанда) | iOS-та мәтін берілмейді — жүйе өзі сұрайды. Android 13+: `POST_NOTIFICATIONS`. Сұрамас бұрын қосымшада: «Сообщим, когда начислятся бонусы и начнутся акции ваших магазинов. / Бонус есептелгенде және дүкендеріңізде акция басталғанда хабарлаймыз.» |

---

## 5. Дүкен беті мәтіндері

### Орысша (негізгі)

**Название (≤30):** SalemBonus
**Подзаголовок App Store (≤30):** Бонусы всех магазинов
**Краткое описание Google Play (≤80):** Бонусные карты всех магазинов в одном приложении: баланс, статус, QR-код.
**Ключевые слова App Store (≤100):** `бонусы,кэшбэк,скидки,карта лояльности,магазины,баллы,акции,qr,накопительная карта`
**Промо-текст App Store (≤170):** Покажите QR-код на кассе — бонусы начислятся сами. Все карты магазинов, баланс и история покупок в одном месте.

**Описание:**

```
SalemBonus — одно приложение для бонусных карт всех партнёрских магазинов. Больше не нужно носить пластиковые карты и помнить, где сколько накоплено.

Как это работает
• Войдите по номеру телефона — без паролей.
• Добавьте магазин: отсканируйте его QR-код камерой или введите код вручную.
• На кассе покажите свой QR-код — бонусы начислятся автоматически. Экран сам станет ярче, чтобы сканер кассы прочитал код.

Что внутри
• Все бонусные карты в одном месте: баланс, ваш статус и процент кэшбэка в каждом магазине.
• Уровни лояльности: Новый, Постоянный, Любимый и VIP — видно, сколько осталось до следующего.
• История операций и электронные чеки покупок.
• Уведомления о начислении и списании бонусов, подарок на день рождения, сгорание бонусов — заранее.
• Каталог магазинов-партнёров рядом с вами.
• Казахский и русский язык, светлая и тёмная тема.

Ваши данные
Мы не продаём данные и не показываем рекламу. Аккаунт можно удалить в любой момент прямо в приложении: Профиль → Удалить аккаунт.

Вопросы и предложения: support@salembonus.kz
```

### Қазақша

**Атауы:** SalemBonus
**App Store подзаголовок:** Барлық дүкеннің бонусы
**Google Play қысқа сипаттама:** Барлық дүкеннің бонус картасы бір қосымшада: баланс, мәртебе, QR код.
**Кілт сөздер:** `бонус,кэшбэк,жеңілдік,жинақ картасы,дүкен,ұпай,акция,qr,сатып алу,лоялдық`
**Промо мәтін:** Кассада QR кодты көрсетіңіз — бонус өзі есептеледі. Барлық дүкен картасы, балансыңыз бен сатып алу тарихы бір жерде.

**Сипаттама:**

```
SalemBonus — серіктес дүкендердің бонус карталарын бір жерге жинайтын қосымша. Пластик картаны алып жүрудің және қай дүкенде қанша жинағаныңызды есте сақтаудың қажеті жоқ.

Қалай жұмыс істейді
• Телефон нөмірімен кіресіз — құпиясөз жоқ.
• Дүкенді қосыңыз: QR кодын камерамен сканерлеңіз не кодын қолмен енгізіңіз.
• Кассада өз QR кодыңызды көрсетіңіз — бонус өзі есептеледі. Касса сканері оқи алуы үшін экран өзі жарықтанады.

Ішінде не бар
• Барлық бонус картасы бір жерде: әр дүкендегі баланс, мәртебеңіз және кэшбэк пайызы.
• Лоялдық деңгейлері: Жаңа, Тұрақты, Сүйікті және VIP — келесі деңгейге қанша қалғаны көрінеді.
• Операциялар тарихы мен сатып алудың электронды чегі.
• Бонус есептелгені мен жұмсалғаны, туған күн сыйлығы, бонустың жануы туралы алдын ала хабарлама.
• Жаныңыздағы серіктес дүкендер каталогы.
• Қазақ және орыс тілі, ашық және қараңғы тақырып.

Деректеріңіз
Деректеріңізді сатпаймыз, жарнама көрсетпейміз. Аккаунтты кез келген уақытта қосымшаның өзінен өшіруге болады: Профиль → Аккаунтты өшіру.

Сұрақ пен ұсыныс: support@salembonus.kz
```

### Скриншоттар туралы

`frontend/store/screenshots/` ішіндегілер дизайн макеттерінен (`design/exports`) жасалған, интерфейсі қазақша. Дүкендер
мұны қабылдайды, бірақ ең сенімдісі — демо аккаунтпен нақты телефоннан не симулятордан түсірілген кадрлар (орысша бетке —
қосымша орыс тілінде). Түсірген соң оларды `design/exports/`-қа сол аттармен салып, `npm run native:assets` іске қос —
жазуы мен өлшемі өзі жасалады.

---

## 6. Жаңарту

1. Кодты өзгерт, `git push` (сайт бұрынғыдай бірден жаңарады).
2. `frontend/android/app/build.gradle`: `versionCode` +1, `versionName`; Xcode: Version / Build.
3. `npm run cap:sync`, сосын 2.3 мен 3.3 — билд пен жүктеу.

Тек веб-код өзгерсе де дүкендегі нұсқа өздігінен жаңармайды — бет қосымшаның ішінде тұр. API өзгерістері ескі нұсқалармен
үйлесімді болуы керек: пайдаланушылар жаңартуды бірден орнатпайды.
