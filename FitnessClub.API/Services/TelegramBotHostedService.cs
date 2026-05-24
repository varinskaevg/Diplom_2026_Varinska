using FitnessClub.API.Data;
using FitnessClub.API.Data.Entities;
using FitnessClub.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FitnessClub.API.Services;

public class TelegramBotHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly TelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopeFactory;  // ✅ ФІКС: тепер ініціалізується

    public TelegramBotHostedService(
        IServiceProvider services,
        IServiceScopeFactory scopeFactory,          // ✅ ФІКС: ін'єктуємо через конструктор
        IConfiguration config,
        ILogger<TelegramBotHostedService> logger)
    {
        _services = services;
        _scopeFactory = scopeFactory;               // ✅ ФІКС: присвоюємо
        _logger = logger;
        _bot = new TelegramBotClient(config["Telegram:BotToken"]!);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🤖 Telegram бот запущено");

        _bot.StartReceiving(
            updateHandler: HandleUpdate,
            errorHandler: HandleError,
            // ✅ Додаємо CallbackQuery для кнопок
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery]
            },
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            // ✅ Обробка натискання inline-кнопок
            if (update.CallbackQuery != null)
            {
                await HandleCallbackQuery(update.CallbackQuery, ct);
                return;
            }

            if (update.Message?.Text == null) return;

            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text.Trim();

            _logger.LogInformation("📨 [{ChatId}]: {Text}", chatId, text);

            if (text.StartsWith("/start")) { await HandleStart(chatId, ct); return; }
            if (text == "/qr" || text == "/myqr") { await HandleGetQr(chatId, ct); return; }
            if (text == "/membership" || text == "/абонемент") { await HandleMembership(chatId, ct); return; }
            if (text == "/schedule" || text == "/розклад") { await HandleSchedule(chatId, ct); return; }
            if (text == "/book" || text == "/запис") { await HandleBookMenu(chatId, ct); return; }  // ✅ НОВА команда
            if (text == "/mybookings" || text == "/мої_записи") { await HandleMyBookings(chatId, ct); return; }  // ✅ НОВА команда
            if (text == "/support" || text == "/підтримка") { await HandleSupportStart(chatId, ct); return; }
            if (text == "/cancel") { await HandleCancelSupport(chatId, ct); return; }
            if (text == "/help" || text == "/допомога") { await HandleHelp(chatId, ct); return; }

            if (await IsInSupportMode(chatId))
            {
                await HandleSupportMessage(chatId, text, ct);
                return;
            }

            if (text.StartsWith("+") || (text.StartsWith("0") && text.Length >= 10))
            {
                await HandlePhoneRegistration(chatId, text, ct);
                return;
            }

            await HandleHelp(chatId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка обробки повідомлення");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  /book — меню вибору заняття
    // ══════════════════════════════════════════════════════════
    private async Task HandleBookMenu(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive, ct);
        if (user == null) { await SendNotLinked(chatId, ct); return; }

        var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id, ct);
        if (client == null) { await _bot.SendMessage(chatId, "❌ Профіль клієнта не знайдено.", cancellationToken: ct); return; }

        var now = DateTime.UtcNow;
        var weekEnd = now.AddDays(7);

        // Заняття де є вільні місця і клієнт ще не записаний
        var bookedScheduleIds = await db.Bookings
            .Where(b => b.ClientId == client.Id && b.Status != "Cancelled")
            .Select(b => b.ScheduleId)
            .ToListAsync(ct);

        var schedules = await db.Schedules
            .Include(s => s.ClassType)
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Bookings)
            .Where(s =>
                s.StartDatetime >= now &&
                s.StartDatetime <= weekEnd &&
                s.Status == "Scheduled" &&
                !bookedScheduleIds.Contains(s.Id))
            .OrderBy(s => s.StartDatetime)
            .Take(20)  // Ліміт для Telegram (інакше занадто багато кнопок)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            await _bot.SendMessage(chatId,
                "📅 Немає доступних занять для запису на найближчий тиждень.\n" +
                "Можливо, ви вже записані на всі заняття або місць немає.",
                cancellationToken: ct);
            return;
        }

        // Будуємо InlineKeyboard — по одній кнопці на рядок
        var buttons = schedules.Select(s =>
        {
            var localTime = s.StartDatetime.ToLocalTime();
            var freeSlots = s.MaxCapacity > 0
                ? s.MaxCapacity - s.Bookings.Count(b => b.Status != "Cancelled")
                : 99;
            var slotsText = s.MaxCapacity > 0 ? $" ({freeSlots} місць)" : "";
            var trainerName = s.Trainer?.User != null
                ? $" · {s.Trainer.User.FirstName}" : "";
            var label = $"{localTime:dd.MM HH:mm} — {s.ClassType.Name}{trainerName}{slotsText}";

            return new[] { InlineKeyboardButton.WithCallbackData(label, $"book:{s.Id}") };
        }).ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons);

        await _bot.SendMessage(
            chatId: chatId,
            text: "📅 *Доступні заняття на тиждень*\n\nОберіть заняття для запису:",
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    // ══════════════════════════════════════════════════════════
    //  /mybookings — мої записи
    // ══════════════════════════════════════════════════════════
    private async Task HandleMyBookings(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive, ct);
        if (user == null) { await SendNotLinked(chatId, ct); return; }

        var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id, ct);
        if (client == null) return;

        var now = DateTime.UtcNow;
        var bookings = await db.Bookings
            .Include(b => b.Schedule).ThenInclude(s => s.ClassType)
            .Include(b => b.Schedule).ThenInclude(s => s.Trainer).ThenInclude(t => t.User)
            .Where(b =>
                b.ClientId == client.Id &&
                b.Status != "Cancelled" &&
                b.Schedule.StartDatetime >= now)
            .OrderBy(b => b.Schedule.StartDatetime)
            .Take(10)
            .ToListAsync(ct);

        if (!bookings.Any())
        {
            await _bot.SendMessage(chatId, "📋 У вас немає активних записів.\n\nЗаписатись: /book", cancellationToken: ct);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📋 *Ваші записи*\n");

        foreach (var b in bookings)
        {
            var localTime = b.Schedule.StartDatetime.ToLocalTime();
            var statusEmoji = b.TelegramStatus switch
            {
                "Pending" => "⏳",
                "Confirmed" => "✅",
                "Rejected" => "❌",
                _ => "✅"
            };
            var statusText = b.TelegramStatus switch
            {
                "Pending" => " \\(очікує підтвердження\\)",
                "Rejected" => " \\(відхилено\\)",
                _ => ""
            };
            var trainerName = b.Schedule.Trainer?.User != null
                ? $"\n👤 {EscapeMd(b.Schedule.Trainer.User.FirstName + " " + b.Schedule.Trainer.User.LastName)}"
                : "";

            sb.AppendLine($"{statusEmoji} *{EscapeMd(b.Schedule.ClassType.Name)}*{statusText}");
            sb.AppendLine($"📅 {EscapeMd(localTime.ToString("dd.MM.yyyy"))} о {EscapeMd(localTime.ToString("HH:mm"))}{trainerName}");
            sb.AppendLine();
        }

        await _bot.SendMessage(
            chatId: chatId,
            text: sb.ToString().TrimEnd(),
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct);
    }

    // ══════════════════════════════════════════════════════════
    //  CallbackQuery — обробка натискання кнопок
    // ══════════════════════════════════════════════════════════
    private async Task HandleCallbackQuery(CallbackQuery query, CancellationToken ct)
    {
        var chatId = query.Message!.Chat.Id;
        var data = query.Data ?? "";

        await _bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        if (data.StartsWith("book:"))
        {
            var scheduleIdStr = data["book:".Length..];
            if (int.TryParse(scheduleIdStr, out var scheduleId))
                await HandleBookingConfirmPrompt(chatId, scheduleId, query.Message.MessageId, ct);
        }
        else if (data.StartsWith("confirm_book:"))
        {
            var scheduleIdStr = data["confirm_book:".Length..];
            if (int.TryParse(scheduleIdStr, out var scheduleId))
                await HandleBookingCreate(chatId, scheduleId, query.Message.MessageId, ct);
        }
        else if (data == "cancel_book")
        {
            await _bot.EditMessageText(
                chatId: chatId,
                messageId: query.Message.MessageId,
                text: "❌ Запис скасовано\\. Оберіть інше заняття: /book",
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: ct);
        }
    }

    // Крок 2: підтвердження запису
    private async Task HandleBookingConfirmPrompt(long chatId, int scheduleId, int messageId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var schedule = await db.Schedules
            .Include(s => s.ClassType)
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == scheduleId, ct);

        if (schedule == null)
        {
            await _bot.EditMessageText(chatId, messageId, "❌ Заняття не знайдено\\.", parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        var freeSlots = schedule.MaxCapacity > 0
            ? schedule.MaxCapacity - schedule.Bookings.Count(b => b.Status != "Cancelled")
            : -1;

        if (freeSlots == 0)
        {
            await _bot.EditMessageText(chatId, messageId,
                "😔 На жаль, місця вже закінчились\\. Оберіть інше заняття: /book",
                parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        var localTime = schedule.StartDatetime.ToLocalTime();
        var trainerName = schedule.Trainer?.User != null
            ? EscapeMd($"{schedule.Trainer.User.FirstName} {schedule.Trainer.User.LastName}") : "—";
        var slotsText = freeSlots > 0 ? $"\n🪑 Вільних місць: {freeSlots}" : "";
        var roomText = !string.IsNullOrEmpty(schedule.Room) ? $"\n🚪 Зал: {EscapeMd(schedule.Room)}" : "";

        var text = $"📋 *Підтвердіть запис*\n\n" +
                   $"🏋️ *{EscapeMd(schedule.ClassType.Name)}*\n" +
                   $"📅 {EscapeMd(localTime.ToString("dd.MM.yyyy"))} о {EscapeMd(localTime.ToString("HH:mm"))}\\-{EscapeMd(schedule.EndDatetime.ToLocalTime().ToString("HH:mm"))}\n" +
                   $"👤 {trainerName}{roomText}{slotsText}\n\n" +
                   $"*Записатись?*";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✅ Записатись", $"confirm_book:{scheduleId}") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "cancel_book") }
        });

        await _bot.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: text,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    // Крок 3: створення бронювання
    private async Task HandleBookingCreate(long chatId, int scheduleId, int messageId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive, ct);
        if (user == null) { await SendNotLinked(chatId, ct); return; }

        var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id, ct);
        if (client == null) return;

        var schedule = await db.Schedules
            .Include(s => s.ClassType)
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == scheduleId, ct);

        if (schedule == null) return;

        // Перевірка дублікату
        var alreadyBooked = await db.Bookings.AnyAsync(b =>
            b.ScheduleId == scheduleId && b.ClientId == client.Id && b.Status != "Cancelled", ct);

        if (alreadyBooked)
        {
            await _bot.EditMessageText(chatId, messageId,
                "ℹ️ Ви вже записані на це заняття\\.",
                parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        // Перевірка місць
        var activeBookings = schedule.Bookings.Count(b => b.Status != "Cancelled");
        if (schedule.MaxCapacity > 0 && activeBookings >= schedule.MaxCapacity)
        {
            await _bot.EditMessageText(chatId, messageId,
                "😔 На жаль, місця вже закінчились\\.",
                parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        // Створюємо бронювання зі статусом Pending
        var booking = new Booking
        {
            ScheduleId = scheduleId,
            ClientId = client.Id,
            Status = "Pending",          // Чекає підтвердження тренера
            Source = "Telegram",
            TelegramStatus = "Pending",
            BookedAt = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        var localTime = schedule.StartDatetime.ToLocalTime();
        await _bot.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: $"✅ *Запит надіслано\\!*\n\n" +
                  $"📅 *{EscapeMd(schedule.ClassType.Name)}*\n" +
                  $"🕐 {EscapeMd(localTime.ToString("dd.MM.yyyy HH:mm"))}\n\n" +
                  $"⏳ Очікуйте підтвердження від тренера\\.\n" +
                  $"Переглянути записи: /mybookings",
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct);

        _logger.LogInformation("✅ Telegram бронювання: ClientId={ClientId}, ScheduleId={ScheduleId}", client.Id, scheduleId);
    }

    // ══════════════════════════════════════════════════════════
    //  /start
    // ══════════════════════════════════════════════════════════
    private async Task HandleStart(long chatId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Users
            .FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive, ct);

        if (existing != null)
        {
            await _bot.SendMessage(
                chatId: chatId,
                text: $"👋 З поверненням, *{EscapeMd(existing.FirstName)}*\\!\n\nНадсилаю ваш QR\\-код\\.\\.\\.",
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: ct);
            await SendQrIfHasMembership(chatId, existing.Id, ct);
            return;
        }

        await _bot.SendMessage(
            chatId: chatId,
            text: "🏋️ *Вітаємо у FitnessClub\\!*\n\n" +
                  "Надішліть ваш *номер телефону* для прив'язки акаунту:\n\n" +
                  "Формат: `\\+380XXXXXXXXX` або `0XXXXXXXXX`",
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct);
    }

    // ══════════════════════════════════════════════════════════
    //  /help
    // ══════════════════════════════════════════════════════════
    private async Task HandleHelp(long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(
            chatId: chatId,
            text: "🏋️ *FitnessClub — Команди бота*\n\n" +
                  "🎫 /qr — Отримати QR\\-код для входу\n" +
                  "💳 /membership — Інформація про абонемент\n" +
                  "📅 /schedule — Розклад занять на тиждень\n" +
                  "📋 /book — Записатись на заняття\n" +       // ✅ НОВЕ
                  "🗒 /mybookings — Мої записи\n" +             // ✅ НОВЕ
                  "📩 /support — Зв'язатися з підтримкою\n" +
                  "❓ /help — Список команд\n\n" +
                  "📱 Надішліть номер телефону для прив'язки акаунту",
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct);
    }

    // ══════════════════════════════════════════════════════════
    //  Реєстрація по телефону
    // ══════════════════════════════════════════════════════════
    private async Task HandlePhoneRegistration(long chatId, string phone, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var last9 = digits.Length >= 9 ? digits[^9..] : digits;

        var allUsers = await db.Users.Where(u => u.IsActive && u.Phone != null).ToListAsync(ct);
        var user = allUsers.FirstOrDefault(u =>
        {
            var d = new string(u.Phone!.Where(char.IsDigit).ToArray());
            return d.EndsWith(last9);
        });

        if (user == null)
        {
            await _bot.SendMessage(chatId,
                "❌ Номер телефону не знайдено в системі\\.\n\nПереконайтесь що номер зареєстрований у клубі, або зверніться до адміністратора\\.",
                parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        user.TelegramChatId = chatId;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await _bot.SendMessage(chatId,
            $"✅ *Акаунт прив'язано\\!*\n\n👤 {EscapeMd(user.FirstName)} {EscapeMd(user.LastName)}\n\nНадсилаю ваш QR\\-код\\.\\.\\.",
            parseMode: ParseMode.MarkdownV2, cancellationToken: ct);

        await SendQrIfHasMembership(chatId, user.Id, ct);
    }

    // ══════════════════════════════════════════════════════════
    //  /qr, /membership, /schedule — без змін (скорочено)
    // ══════════════════════════════════════════════════════════
    private async Task HandleGetQr(long chatId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive, ct);
        if (user == null) { await SendNotLinked(chatId, ct); return; }
        await _bot.SendMessage(chatId, "🔄 Генерую QR\\-код\\.\\.\\.", parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
        await SendQrIfHasMembership(chatId, user.Id, ct);
    }

    private async Task HandleMembership(long chatId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive, ct);
        if (user == null) { await SendNotLinked(chatId, ct); return; }

        var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id, ct);
        if (client == null) { await _bot.SendMessage(chatId, "❌ Профіль клієнта не знайдено\\.", parseMode: ParseMode.MarkdownV2, cancellationToken: ct); return; }

        var memberships = await db.Memberships
            .Where(m => m.ClientId == client.Id)
            .OrderByDescending(m => m.StartDate)
            .Take(5)
            .ToListAsync(ct);

        if (!memberships.Any())
        {
            await _bot.SendMessage(chatId, "💳 *Абонементи*\n\nУ вас ще немає абонементів\\.\nЗверніться на рецепцію для придбання\\. 🏋️", parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        var typeIds = memberships.Select(m => m.MembershipTypeId).Distinct().ToList();
        var types = await db.MembershipTypes.Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t, ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("💳 *Ваші абонементи*\n");

        foreach (var m in memberships)
        {
            var typeName = types.TryGetValue(m.MembershipTypeId, out var t) ? t.Name : "—";
            var isActive = m.Status == "Active" && !m.IsExpired;
            var statusEmoji = m.Status switch { "Active" => isActive ? "✅" : "⏰", "Frozen" => "❄️", "Cancelled" => "❌", _ => "⏰" };
            sb.AppendLine($"{statusEmoji} *{EscapeMd(typeName)}*");
            sb.AppendLine($"📅 {EscapeMd(m.StartDate.ToString("dd.MM.yyyy"))} — {EscapeMd(m.EndDate.ToString("dd.MM.yyyy"))}");
            if (isActive)
            {
                var daysLeft = m.EndDate.DayNumber - today.DayNumber;
                sb.AppendLine($"⏳ Залишилось *{daysLeft} дн\\.*");
            }
            sb.AppendLine();
        }

        await _bot.SendMessage(chatId, sb.ToString().TrimEnd(), parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
    }

    private async Task HandleSchedule(long chatId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive, ct);
        if (user == null) { await SendNotLinked(chatId, ct); return; }

        var now = DateTime.UtcNow;
        var weekEnd = now.AddDays(7);
        var schedules = await db.Schedules
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Where(s => s.StartDatetime >= now && s.StartDatetime <= weekEnd && s.Status == "Scheduled")
            .OrderBy(s => s.StartDatetime)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            await _bot.SendMessage(chatId, "📅 *Розклад занять*\n\nНа найближчий тиждень занять не заплановано\\.", parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            return;
        }

        var classTypeIds = schedules.Select(s => s.ClassTypeId).Distinct().ToList();
        var classTypes = await db.ClassTypes.Where(c => classTypeIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var grouped = schedules.GroupBy(s => s.StartDatetime.ToLocalTime().Date).OrderBy(g => g.Key);
        var dayNames = new Dictionary<DayOfWeek, string> { { DayOfWeek.Monday, "Понеділок" }, { DayOfWeek.Tuesday, "Вівторок" }, { DayOfWeek.Wednesday, "Середа" }, { DayOfWeek.Thursday, "Четвер" }, { DayOfWeek.Friday, "П'ятниця" }, { DayOfWeek.Saturday, "Субота" }, { DayOfWeek.Sunday, "Неділя" } };

        foreach (var day in grouped)
        {
            var dayName = dayNames.TryGetValue(day.Key.DayOfWeek, out var dn) ? dn : "";
            var isToday = day.Key == DateTime.Today;
            var daySb = new System.Text.StringBuilder();
            daySb.AppendLine($"📅 *{EscapeMd(dayName)}, {EscapeMd(day.Key.ToString("dd.MM"))}*{(isToday ? " 📍" : "")}\n");

            foreach (var s in day.OrderBy(s => s.StartDatetime))
            {
                var className = classTypes.TryGetValue(s.ClassTypeId, out var cn) ? cn : "Заняття";
                var trainerName = s.Trainer?.User != null ? $"{s.Trainer.User.FirstName} {s.Trainer.User.LastName}" : "—";
                var localStart = s.StartDatetime.ToLocalTime();
                var localEnd = s.EndDatetime.ToLocalTime();
                var capacity = s.MaxCapacity > 0 ? $" \\(до {s.MaxCapacity} осіб\\)" : "";
                daySb.AppendLine($"🕐 `{localStart:HH:mm}\\-{localEnd:HH:mm}` — *{EscapeMd(className)}*{capacity}");
                daySb.AppendLine($"👤 {EscapeMd(trainerName)}\n");
            }

            await _bot.SendMessage(chatId, daySb.ToString().TrimEnd(), parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            await Task.Delay(200, ct);
        }

        // ✅ Підказка після розкладу
        await _bot.SendMessage(chatId,
            "📋 Хочете записатись на заняття? Натисніть /book",
            cancellationToken: ct);
    }

    // ══════════════════════════════════════════════════════════
    //  Support
    // ══════════════════════════════════════════════════════════
    private async Task HandleSupportStart(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId && u.IsActive, ct);
        if (user == null) { await SendNotLinked(chatId, ct); return; }

        var session = await db.SupportSessions.FirstOrDefaultAsync(s => s.ChatId == chatId && s.IsActive, ct);
        if (session == null)
        {
            session = new SupportSession { ChatId = chatId, UserId = user.Id, IsActive = true, StartedAt = DateTime.UtcNow };
            db.SupportSessions.Add(session);
            await db.SaveChangesAsync(ct);
        }
        await _bot.SendMessage(chatId, "📩 Напишіть ваше питання. Для виходу — /cancel", cancellationToken: ct);
    }

    private async Task<bool> IsInSupportMode(long chatId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SupportSessions.AnyAsync(s => s.ChatId == chatId && s.IsActive);
    }

    private async Task HandleSupportMessage(long chatId, string text, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.SupportSessions.FirstOrDefaultAsync(s => s.ChatId == chatId && s.IsActive, ct);
        if (session == null) return;

        db.SupportMessages.Add(new SupportMessage { SessionId = session.Id, Text = text, IsFromAdmin = false, CreatedAt = DateTime.UtcNow });
        session.UnreadCount++;
        session.LastMessageAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await _bot.SendMessage(chatId, "✅ Відправлено", cancellationToken: ct);
    }

    private async Task HandleCancelSupport(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.SupportSessions.FirstOrDefaultAsync(s => s.ChatId == chatId && s.IsActive, ct);
        if (session != null)
        {
            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await _bot.SendMessage(chatId, "✅ Сесію підтримки завершено. Дякуємо!", cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(chatId, "ℹ️ У вас немає активної сесії підтримки.", cancellationToken: ct);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  QR
    // ══════════════════════════════════════════════════════════
    private async Task SendQrIfHasMembership(long chatId, int userId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegram = scope.ServiceProvider.GetRequiredService<ITelegramService>();

        var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (client == null) { await telegram.SendMessageAsync(chatId, "ℹ️ Профіль клієнта не знайдено\\. Зверніться до адміністратора\\."); return; }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var activeMembership = await db.Memberships
            .Where(m => m.ClientId == client.Id && m.Status == "Active" && m.EndDate >= today)
            .OrderByDescending(m => m.EndDate)
            .FirstOrDefaultAsync(ct);

        if (activeMembership == null)
        {
            await telegram.SendMessageAsync(chatId, "❌ *Активного абонементу не знайдено\\.*\n\nЗверніться на рецепцію для придбання абонементу\\. 🏋️");
            return;
        }

        var membershipType = await db.MembershipTypes.FindAsync(new object[] { activeMembership.MembershipTypeId }, ct);
        var user = await db.Users.FindAsync(new object[] { userId }, ct);

        await telegram.SendMembershipQrAsync(
            chatId: chatId,
            clientName: $"{user!.FirstName} {user.LastName}",
            clientId: client.Id,
            membershipType: membershipType?.Name ?? "Абонемент",
            expiryDate: activeMembership.EndDate.ToDateTime(TimeOnly.MinValue));
    }

    // ══════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════
    private async Task SendNotLinked(long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId,
            "❌ Акаунт не прив'язано\\.\n\nНадішліть ваш номер телефону для прив'язки\\.\nФормат: `\\+380XXXXXXXXX` або `0XXXXXXXXX`",
            parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
    }

    private static string EscapeMd(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var c in new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' })
            text = text.Replace(c.ToString(), $"\\{c}");
        return text;
    }

    private Task HandleError(ITelegramBotClient bot, Exception ex, HandleErrorSource source, CancellationToken ct)
    {
        _logger.LogError(ex, "❌ Помилка Telegram бота");
        return Task.CompletedTask;
    }
}