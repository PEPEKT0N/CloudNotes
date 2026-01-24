using System;
using System.Threading;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CloudNotes.Desktop.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<NoteTag> NoteTags { get; set; } = null!;
    public DbSet<Folder> Folders { get; set; } = null!;
    public DbSet<FlashcardStats> FlashcardStats { get; set; } = null!;
    public DbSet<FavoriteTagCombo> FavoriteTagCombos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Настройка Many-to-Many связи Note ↔ Tag через NoteTag
        modelBuilder.Entity<NoteTag>()
            .HasKey(nt => new { nt.NoteId, nt.TagId });

        modelBuilder.Entity<NoteTag>()
            .HasOne(nt => nt.Note)
            .WithMany(n => n.NoteTags)
            .HasForeignKey(nt => nt.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NoteTag>()
            .HasOne(nt => nt.Tag)
            .WithMany(t => t.NoteTags)
            .HasForeignKey(nt => nt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // Настройка Folder
        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Name).IsRequired().HasMaxLength(255);
        });

        // Индекс для быстрого поиска статистики карточки
        modelBuilder.Entity<FlashcardStats>()
            .HasIndex(fs => new { fs.UserEmail, fs.QuestionHash })
            .IsUnique();

        // Индекс для избранных комбинаций тегов по пользователю
        modelBuilder.Entity<FavoriteTagCombo>()
            .HasIndex(f => f.UserEmail);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Автоматически устанавливаем/обновляем CreatedAt и UpdatedAt для всех заметок в UTC
        var noteEntries = ChangeTracker.Entries<Note>();

        foreach (var entry in noteEntries)
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;

                if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
            }
        }

        // Автоматически устанавливаем/обновляем CreatedAt и UpdatedAt для всех папок в UTC
        var folderEntries = ChangeTracker.Entries<Folder>();

        foreach (var entry in folderEntries)
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;

                if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
