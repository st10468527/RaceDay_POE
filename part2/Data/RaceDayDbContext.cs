using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace RaceDay.Api.Data;

// The DbContext is the bridge between our C# classes and the SQL Server database.
public class RaceDayDbContext : DbContext
{
    // The connection settings are handed to us from Program.cs (we set that up in the next step)
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    // Each DbSet becomes one table in the database
    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventEnrolment> EventEnrolments => Set<EventEnrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Here we set column types, unique rules, defaults, CHECK limits and relationships
    // so the generated tables match the Part 1 SQL script.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organiser>(e =>
        {
            e.Property(x => x.FullName).HasColumnType("varchar(100)");
            e.Property(x => x.Email).HasColumnType("varchar(150)");
            e.Property(x => x.PasswordHash).HasColumnType("varchar(255)");
            e.Property(x => x.PhoneNumber).HasColumnType("varchar(20)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.Email).IsUnique();             // the same email cannot register twice
        });

        modelBuilder.Entity<Participant>(e =>
        {
            e.Property(x => x.FullName).HasColumnType("varchar(100)");
            e.Property(x => x.Email).HasColumnType("varchar(150)");
            e.Property(x => x.PasswordHash).HasColumnType("varchar(255)");
            e.Property(x => x.PhoneNumber).HasColumnType("varchar(20)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.Property(x => x.Name).HasColumnType("varchar(150)");
            e.Property(x => x.Description).HasColumnType("varchar(1000)");
            e.Property(x => x.EventDate).HasColumnType("date");
            e.Property(x => x.Location).HasColumnType("varchar(150)");
            e.Property(x => x.EventType).HasColumnType("varchar(10)");
            e.Property(x => x.Distance).HasColumnType("decimal(6,2)");
            e.Property(x => x.ElevationGain).HasColumnType("decimal(6,2)");
            e.Property(x => x.StartPoint).HasColumnType("varchar(150)");
            e.Property(x => x.EndPoint).HasColumnType("varchar(150)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            // Only Run, Walk or Cycle are allowed (the CHECK constraint from Part 1)
            e.ToTable(t => t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            // Restrict = no cascade delete, same as our SQL script
            e.HasOne(x => x.Organiser).WithMany(o => o.Events)
                .HasForeignKey(x => x.OrganiserID).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasColumnType("varchar(100)");
            e.Property(x => x.Distance).HasColumnType("decimal(6,2)");
            e.Property(x => x.EntryFee).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventID).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventEnrolment>(e =>
        {
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            // A participant can only enter the same category once
            e.HasIndex(x => new { x.ParticipantID, x.CategoryID }).IsUnique();
            e.HasOne(x => x.Participant).WithMany(p => p.Enrolments)
                .HasForeignKey(x => x.ParticipantID).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryID).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.Property(x => x.FinishTime).HasColumnType("time");
            // One-to-one: WithOne makes EnrolmentID unique, so an enrolment has at most one result
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentID).OnDelete(DeleteBehavior.Restrict);
            // CapturedBy points at the Organisers table (we name the link by hand)
            e.HasOne(x => x.CapturedByOrganiser).WithMany()
                .HasForeignKey(x => x.CapturedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("decimal(8,2)");
            e.Property(x => x.PaymentStatus).HasColumnType("varchar(20)").HasDefaultValue("Pending");
            e.Property(x => x.PaymentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(x => x.PaymentMethod).HasColumnType("varchar(50)");
            e.ToTable(t => t.HasCheckConstraint("CK_Payments_PaymentStatus", "[PaymentStatus] IN ('Pending', 'Paid', 'Failed')"));
            e.HasOne(x => x.Enrolment).WithOne(en => en.Payment)
                .HasForeignKey<Payment>(x => x.EnrolmentID).OnDelete(DeleteBehavior.Restrict);
        });
    }
}