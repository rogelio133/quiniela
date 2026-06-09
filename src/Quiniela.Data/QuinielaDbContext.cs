using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Quiniela.Data.Entities;

namespace Quiniela.Data;

public class QuinielaDbContext(DbContextOptions<QuinielaDbContext> options)
    : IdentityDbContext<User, IdentityRole<int>, int>(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Pool> Pools => Set<Pool>();
    public DbSet<PoolMember> PoolMembers => Set<PoolMember>();
    public DbSet<Prediction> Predictions => Set<Prediction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // required: configures all Identity tables

        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(100);
        });

        modelBuilder.Entity<Team>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(100);
            e.Property(t => t.FlagCode).HasMaxLength(10);
            e.Property(t => t.ShortCode).HasMaxLength(3);
            e.Property(t => t.GroupCode).HasColumnType("char(1)");
        });

        modelBuilder.Entity<Match>(e =>
        {
            e.HasOne(m => m.HomeTeam)
                .WithMany(t => t.HomeMatches)
                .HasForeignKey(m => m.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.AwayTeam)
                .WithMany(t => t.AwayMatches)
                .HasForeignKey(m => m.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(m => m.Venue).HasMaxLength(100);
            e.Property(m => m.GroupCode).HasColumnType("char(1)");
        });

        modelBuilder.Entity<Pool>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.JoinCode).HasMaxLength(8);
            e.HasIndex(p => p.JoinCode).IsUnique();
        });

        modelBuilder.Entity<PoolMember>(e =>
        {
            e.HasKey(pm => new { pm.PoolId, pm.UserId });
            // Restrict to avoid multiple cascade paths (Users→Pools→PoolMembers AND Users→PoolMembers)
            e.HasOne(pm => pm.User)
                .WithMany(u => u.PoolMemberships)
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Prediction>(e =>
        {
            e.Property(p => p.PredOutcome).HasColumnType("char(1)");
            e.HasIndex(p => new { p.UserId, p.PoolId, p.MatchId }).IsUnique();
            // Restrict to avoid multiple cascade paths (Users→Pools→Predictions AND Users→Predictions)
            e.HasOne(p => p.User)
                .WithMany(u => u.Predictions)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Pool)
                .WithMany(pl => pl.Predictions)
                .HasForeignKey(p => p.PoolId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
