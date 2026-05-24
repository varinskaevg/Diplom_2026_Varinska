using FitnessClub.API.Data;
using FitnessClub.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FitnessClub.API.BackgroundServices;

public class MembershipExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MembershipExpiryBackgroundService> _logger;
    private static readonly TimeOnly RunAtTime = new(10, 0);

    public MembershipExpiryBackgroundService(
        IServiceProvider services,
        ILogger<MembershipExpiryBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔔 MembershipExpiryService запущено");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = DateTime.Today.Add(RunAtTime.ToTimeSpan());
            if (now > nextRun) nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;
            _logger.LogInformation(
                "⏰ Наступна перевірка: {NextRun}", nextRun);

            try { await Task.Delay(delay, stoppingToken); }
            catch (TaskCanceledException) { break; }

            await CheckAndSendReminders(stoppingToken);
        }
    }

    private async Task CheckAndSendReminders(CancellationToken ct)
    {
        _logger.LogInformation("📋 Перевірка абонементів...");

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegram = scope.ServiceProvider.GetRequiredService<ITelegramService>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var notifyDays = new[] { 7, 3, 1 };
        var totalSent = 0;

        foreach (var days in notifyDays)
        {
            var targetDate = today.AddDays(days);

            var expiringMemberships = await db.Memberships
                .Include(m => m.Client)
                    .ThenInclude(c => c.User)
                .Where(m =>
                    m.EndDate == targetDate &&
                    m.Status == "Active" &&
                    m.Client.User.TelegramChatId != null)
                .ToListAsync(ct);

            _logger.LogInformation(
                "  Через {Days} дн. закінчується: {Count}", days, expiringMemberships.Count);

            foreach (var membership in expiringMemberships)
            {
                try
                {
                    var chatId = membership.Client.User.TelegramChatId!.Value;
                    var clientName = $"{membership.Client.User.FirstName} {membership.Client.User.LastName}";

                    await telegram.SendExpiryReminderAsync(
                        chatId: chatId,
                        clientName: clientName,
                        expiryDate: membership.EndDate.ToDateTime(TimeOnly.MinValue),
                        daysLeft: days);

                    totalSent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Помилка надсилання нагадування клієнту {ClientId}",
                        membership.ClientId);
                }
            }
        }

        _logger.LogInformation("✅ Відправлено нагадувань: {Total}", totalSent);
    }
}