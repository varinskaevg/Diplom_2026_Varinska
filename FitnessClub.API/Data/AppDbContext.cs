using FitnessClub.API.Data.Entities;
using FitnessClub.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FitnessClub.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Trainer> Trainers { get; set; }
    public DbSet<MembershipType> MembershipTypes { get; set; }
    public DbSet<Membership> Memberships { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<ClassType> ClassTypes { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Visit> Visits { get; set; }

    // ── ІСНУЮЧІ ──
    public DbSet<QrToken> QrTokens { get; set; }
    public DbSet<BotLog> BotLogs { get; set; }
    public DbSet<BroadcastHistory> BroadcastHistories { get; set; }
    public DbSet<SupportSession> SupportSessions { get; set; }
    public DbSet<SupportMessage> SupportMessages { get; set; }

    // ── НОВІ: прив'язка клієнтів до тренера ──
    public DbSet<TrainerClient> TrainerClients { get; set; }
    public DbSet<TrainerClientPayment> TrainerClientPayments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>().HasKey(r => r.Id);
        modelBuilder.Entity<ClassType>().ToTable("class_types");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId);

        modelBuilder.Entity<Client>()
            .HasOne(c => c.User)
            .WithOne(u => u.Client)
            .HasForeignKey<Client>(c => c.UserId);

        modelBuilder.Entity<Trainer>()
            .HasOne(t => t.User)
            .WithOne(u => u.Trainer)
            .HasForeignKey<Trainer>(t => t.UserId);

        modelBuilder.Entity<Membership>()
            .HasOne(m => m.Client)
            .WithMany(c => c.Memberships)
            .HasForeignKey(m => m.ClientId);

        modelBuilder.Entity<Membership>()
            .HasOne(m => m.MembershipType)
            .WithMany(mt => mt.Memberships)
            .HasForeignKey(m => m.MembershipTypeId);

        modelBuilder.Entity<Membership>()
            .Property(m => m.MembershipTypeId)
            .HasColumnName("membership_type_id");

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Client)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.ClientId);

        modelBuilder.Entity<Schedule>()
            .HasOne(s => s.Trainer)
            .WithMany(t => t.Schedules)
            .HasForeignKey(s => s.TrainerId);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.ScheduleId, b.ClientId })
            .IsUnique();

        modelBuilder.Entity<Visit>()
            .HasOne(v => v.Client)
            .WithMany(c => c.Visits)
            .HasForeignKey(v => v.ClientId);

        // ── QrToken ──────────────────────────────────────────
        modelBuilder.Entity<QrToken>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.HasIndex(q => q.Token).IsUnique();
            entity.HasIndex(q => new { q.ClientId, q.IsUsed });

            entity.HasOne(q => q.Client)
                .WithMany()
                .HasForeignKey(q => q.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(q => q.Visit)
                .WithMany()
                .HasForeignKey(q => q.VisitId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        // ── TrainerClient ─────────────────────────────────────
        modelBuilder.Entity<TrainerClient>(entity =>
        {
            entity.HasKey(tc => tc.Id);

            // Унікальна активна прив'язка: один клієнт — одному тренеру (активна)
            entity.HasIndex(tc => new { tc.TrainerId, tc.ClientId, tc.IsActive });

            entity.HasOne(tc => tc.Trainer)
                .WithMany()
                .HasForeignKey(tc => tc.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tc => tc.Client)
                .WithMany()
                .HasForeignKey(tc => tc.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(tc => tc.Rate)
                .HasColumnType("decimal(18,2)");

            entity.Property(tc => tc.PaymentType)
                .HasMaxLength(20)
                .HasDefaultValue("single");
        });

        // ── TrainerClientPayment ──────────────────────────────
        modelBuilder.Entity<TrainerClientPayment>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.HasOne(p => p.TrainerClient)
                .WithMany(tc => tc.Payments)
                .HasForeignKey(p => p.TrainerClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            entity.Property(p => p.PaymentMethod)
                .HasMaxLength(20)
                .HasDefaultValue("Cash");
        });

        // ── Snake_case для всіх таблиць і колонок ──
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()));

            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName(
                    StoreObjectIdentifier.Table(entity.GetTableName()!, null));

                if (!string.IsNullOrEmpty(columnName) &&
                    property.GetColumnName() == property.Name)
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToLowerInvariant(name[0]));
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                sb.Append(name[i]);
            }
        }
        return sb.ToString();
    }
}