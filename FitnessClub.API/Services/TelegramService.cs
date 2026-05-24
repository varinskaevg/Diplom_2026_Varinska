using FitnessClub.API.Data;
using FitnessClub.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QRCoder;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FitnessClub.API.Services;

public interface ITelegramService
{
    Task SendMembershipQrAsync(long chatId, string clientName,
        int clientId, string membershipType, DateTime expiryDate);

    Task SendExpiryReminderAsync(long chatId, string clientName,
        DateTime expiryDate, int daysLeft);

    Task SendWelcomeAsync(long chatId, string clientName);

    Task SendMessageAsync(long chatId, string text);
}

public class TelegramService : ITelegramService
{
    private readonly TelegramBotClient _bot;
    private readonly ILogger<TelegramService> _logger;
    private readonly IServiceProvider _services;

    // Базовий URL API — для формування посилання в QR (рецепція сканує цей URL)
    private readonly string _apiBaseUrl;

    public TelegramService(
        IConfiguration config,
        ILogger<TelegramService> logger,
        IServiceProvider services)
    {
        _logger = logger;
        _services = services;
        _bot = new TelegramBotClient(config["Telegram:BotToken"]!);
        // Наприклад: https://yourdomain.com або http://192.168.1.100:5000
        _apiBaseUrl = config["App:BaseUrl"] ?? "https://localhost:5001";
    }

    // ══════════════════════════════════════════════════════════
    //  QR при купівлі абонементу / запиті /qr
    // ══════════════════════════════════════════════════════════
    public async Task SendMembershipQrAsync(
        long chatId,
        string clientName,
        int clientId,
        string membershipType,
        DateTime expiryDate)
    {
        try
        {
            var firstName = clientName.Split(' ').FirstOrDefault() ?? clientName;

            // ── Генеруємо безпечний токен ──────────────────────────
            var token = await GenerateOrReuseTokenAsync(clientId);

            // QR містить URL який сканує термінал на рецепції
            // Формат: https://yourdomain.com/api/qr/scan?token=<GUID>
            var qrContent = $"{_apiBaseUrl}/api/qr/scan?token={token}";
            var qrBytes = GenerateQrPng(qrContent);

            // ── Текст повідомлення ─────────────────────────────────
            var caption =
                $"🏋️ *FitnessClub* — Ваш QR\\-код для входу\n\n" +
                $"👤 *{EscapeMd(firstName)}*\n" +
                $"🎫 {EscapeMd(membershipType)}\n" +
                $"📅 Дійсний до: *{EscapeMd(expiryDate.ToString("dd.MM.yyyy"))}*\n\n" +
                $"🔐 *QR\\-код діє 24 години*\n" +
                $"Для нового коду надішліть /qr\n\n" +
                $"📲 _Покажіть цей QR\\-код на терміналі при вході_";

            using var ms = new MemoryStream(qrBytes);
            await _bot.SendPhoto(
                chatId: chatId,
                photo: InputFile.FromStream(ms, "qr.png"),
                caption: caption,
                parseMode: ParseMode.MarkdownV2);

            _logger.LogInformation(
                "✅ Безпечний QR надіслано клієнту {ClientId}, токен діє до {Expiry}",
                clientId, DateTime.UtcNow.AddHours(24));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Помилка надсилання QR клієнту {ClientId}", clientId);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Генерація або повторне використання токену
    //  Логіка: якщо є невикористаний токен ще дійсний — повертаємо його.
    //  Інакше — анулюємо старі та створюємо новий.
    // ══════════════════════════════════════════════════════════
    private async Task<string> GenerateOrReuseTokenAsync(int clientId)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Шукаємо дійсний невикористаний токен для цього клієнта
        var existing = await db.QrTokens
            .Where(q => q.ClientId == clientId
                     && !q.IsUsed
                     && q.ExpiresAt > now)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            _logger.LogInformation(
                "♻️ Повертаємо існуючий токен для ClientId={ClientId}, діє до {Expiry}",
                clientId, existing.ExpiresAt);
            return existing.Token;
        }

        // Анулюємо всі старі токени клієнта (прострочені або невикористані)
        var oldTokens = await db.QrTokens
            .Where(q => q.ClientId == clientId && !q.IsUsed)
            .ToListAsync();

        if (oldTokens.Any())
        {
            db.QrTokens.RemoveRange(oldTokens);
            _logger.LogInformation(
                "🗑️ Видалено {Count} старих токенів для ClientId={ClientId}",
                oldTokens.Count, clientId);
        }

        // Створюємо новий токен
        var newToken = new QrToken
        {
            ClientId = clientId,
            Token = Guid.NewGuid().ToString("N"), // 32 hex символи без дефісів
            CreatedAt = now,
            ExpiresAt = now.AddHours(24),
            IsUsed = false
        };

        db.QrTokens.Add(newToken);
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "🔑 Новий токен створено для ClientId={ClientId}, діє до {Expiry}",
            clientId, newToken.ExpiresAt);

        return newToken.Token;
    }

    // ══════════════════════════════════════════════════════════
    //  Нагадування про закінчення
    // ══════════════════════════════════════════════════════════
    public async Task SendExpiryReminderAsync(
        long chatId,
        string clientName,
        DateTime expiryDate,
        int daysLeft)
    {
        try
        {
            var firstName = clientName.Split(' ').FirstOrDefault() ?? clientName;

            var emoji = daysLeft <= 1 ? "🚨" : daysLeft <= 3 ? "⚠️" : "ℹ️";
            var urgency = daysLeft switch
            {
                1 => "ОСТАННІЙ ДЕНЬ\\!",
                <= 3 => "Залишилось мало часу\\!",
                _ => "Нагадування"
            };

            var whenText = daysLeft == 1
                ? "сьогодні"
                : $"через *{daysLeft} дні*";

            var text =
                $"{emoji} *FitnessClub — {urgency}*\n\n" +
                $"👤 {EscapeMd(firstName)}, ваш абонемент закінчується {whenText}\n" +
                $"📅 Дата закінчення: *{EscapeMd(expiryDate.ToString("dd.MM.yyyy"))}*\n\n" +
                $"👉 Зверніться на рецепцію або напишіть нам для продовження абонементу\\.\n\n" +
                $"Гарних тренувань\\! 💪";

            await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.MarkdownV2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Помилка надсилання нагадування в Telegram chat {ChatId}", chatId);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Привітання при реєстрації
    // ══════════════════════════════════════════════════════════
    public async Task SendWelcomeAsync(long chatId, string clientName)
    {
        try
        {
            var firstName = clientName.Split(' ').FirstOrDefault() ?? clientName;

            var text =
                $"🏋️ *Вітаємо у FitnessClub, {EscapeMd(firstName)}\\!*\n\n" +
                $"Ваш профіль успішно створено\\.\n\n" +
                $"Після придбання абонементу ви отримаєте QR\\-код для входу прямо сюди\\.\n\n" +
                $"Гарних тренувань\\! 💪";

            await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.MarkdownV2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Помилка надсилання привітання в Telegram chat {ChatId}", chatId);
        }
    }

    public async Task SendMessageAsync(long chatId, string text)
    {
        try
        {
            await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.MarkdownV2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Помилка надсилання повідомлення в Telegram");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Генерація QR PNG
    // ══════════════════════════════════════════════════════════
    private static byte[] GenerateQrPng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var code = new PngByteQRCode(data);
        return code.GetGraphic(10);
    }

    // MarkdownV2 escape
    private static string EscapeMd(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var specialChars = new[]
        {
            '_', '*', '[', ']', '(', ')', '~', '`', '>',
            '#', '+', '-', '=', '|', '{', '}', '.', '!'
        };
        foreach (var c in specialChars)
            text = text.Replace(c.ToString(), $"\\{c}");
        return text;
    }
}