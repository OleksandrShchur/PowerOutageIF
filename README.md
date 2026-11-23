# ⚡ PowerOutageIF — Telegram-бот моніторингу графіків відключень світла (Прикарпаттяобленерго)

Telegram-бот на платформі **.NET 8 (ASP.NET Core Minimal API)** та **SQL Server** для оперативного моніторингу графіків погодинних вимкнень (ГПВ) електроенергії в Івано-Франківській області з офіційного сервісу [svitlo.oe.if.ua](https://svitlo.oe.if.ua/).

Архітектура оптимізована під високе навантаження (**10 000+ користувачів**) та безкоштовний хостинг **MonsterASP.net**.

---

## 🎯 Основні можливості

- **⚡ Пряме підключення до API обленерго**: бот працює безпосередньо з бекендом `be-svitlo.oe.if.ua`, отримуючи точні дані у JSON без важкого парсингу.
- **🔄 Сповіщення про зміни протягом дня**: при зміні розкладу (додавання/скасування вимкнень, зміна годин) підписані користувачі отримують детальне сповіщення із різницею («diff»).
- **📢 Сповіщення про графік на завтра**: як тільки обленерго публікує розклад на наступний день, користувачі отримують миттєвий пуш.
- **📊 24-годинна візуальна шкала**: наочне відображення доби (`🟩` — є світло, `🟥` — вимкнення) у 2 компактні ряди по 12 годин.
- **🟢 Поточний стан та зворотний відлік**: відображення поточного статусу та часу до найближчого вимкнення чи увімкнення світла.
- **🌙 Тихий нічний режим (23:00 — 07:00)**: сповіщення вночі надсилаються без звуку (`disable_notification: true`).
- **🚀 Масштабованість на 10k+ користувачів**:
  - Асинхронна черга розсилки `System.Threading.Channels.Channel`.
  - Вбудований **Rate Limiter на 25 повідомлень/сек** (з запасом вкладається в ліміт Telegram 30 msg/s).
  - Автоматична деактивація підписників, які заблокували бота (помилка `403 Forbidden`).
  - Обробка `429 Too Many Requests` з урахуванням `retry_after`.
- **🔄 Гнучкість (Dual Mode)**:
  - **Long Polling**: за замовчуванням для швидкої локальної розробки (без доменів і SSL).
  - **Webhook**: для хостингу на MonsterASP.net / продакшену.
- **💓 Keep-Alive ендпоінт (`GET /health`)**: для захисту від засинання (Idle Timeout) на MonsterASP.net.

---

## 🛠 Стек технологій

- **Платформа**: .NET 8 (ASP.NET Core Web API)
- **Telegram API**: `Telegram.Bot` v22
- **База даних**: Microsoft SQL Server / LocalDB
- **ORM**: Entity Framework Core 8.0 (`Microsoft.EntityFrameworkCore.SqlServer`)
- **Тестування**: xUnit, EF Core InMemory, інтеграційні тести API та SQL Server (13 тестів)

---

## ⚙️ Конфігурація (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PowerOutageIfDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "BotSettings": {
    "BotToken": "ВАШ_ТОКЕН_З_BOTFATHER",
    "UseWebhook": false,
    "WebhookUrl": "https://your-site.monsterasp.net/api/telegram/webhook",
    "SecretToken": "your_secure_random_token"
  },
  "OblenergoSettings": {
    "BaseUrl": "https://be-svitlo.oe.if.ua/",
    "PollingIntervalSeconds": 120
  }
}
```

---

## 💻 Локальний запуск (Long Polling)

1. Вкажіть токен у `PowerOutageIF/appsettings.json` (`UseWebhook: false`).
2. Запустіть проєкт:
   ```bash
   dotnet run --project PowerOutageIF
   ```
3. База даних SQL Server LocalDB створиться автоматично, а бот почне прослуховувати команди.

---

## 🌐 Розгортання на MonsterASP.net (Free Tier)

### Крок 1. Створення сайту та бази даних на MonsterASP
1. Зареєструйтесь на [MonsterASP.net](https://www.monsterasp.net/) та створіть безкоштовний сайт (наприклад: `poweroutage.monsterasp.net`).
2. Створіть базу даних **MS SQL** у панелі MonsterASP та скопіюйте рядок підключення (`ConnectionStrings:DefaultConnection`).

### Крок 2. Налаштування Webhook
У `appsettings.json` або змінних середовища MonsterASP вкажіть:
- `"UseWebhook": true`
- `"WebhookUrl": "https://poweroutage.monsterasp.net/api/telegram/webhook"`
- `"SecretToken": "будь-який_надійний_пароль"`

### Крок 3. Публікація коду
```bash
cd PowerOutageIF
dotnet publish -c Release -o ./publish
```
Завантажте вміст папки `./publish` на MonsterASP через FTP або WebDeploy.

### Крок 4. Захист від засинання (Keep-Alive для безкоштовного тарифу)
MonsterASP на безкоштовному тарифі вивантажує сайт з пам'яті через 15–20 хвилин бездіяльності (Idle Timeout).
1. Зареєструйтесь на безкоштовному сервісі моніторингу (наприклад, [UptimeRobot](https://uptimerobot.com/)).
2. Створіть HTTP-монітор для URL:
   `https://poweroutage.monsterasp.net/health`
3. Встановіть інтервал перевірки: **кожні 5 хвилин**.
   - Сервіс повертає `200 OK` зі статусом `Healthy`.
   - Це гарантує, що додаток працює 24/7 і фоновий сервіс опитування обленерго ніколи не засне.

---

## 🧪 Запуск тестів
```bash
dotnet test
```
