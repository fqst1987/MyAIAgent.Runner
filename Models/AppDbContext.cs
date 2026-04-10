using Microsoft.EntityFrameworkCore;
using MyAIAgent.Runner;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public AppDbContext()
    {
    }

    public DbSet<CsdQuestionaireSystems> CsdQuestionaireSystems { get; set; }
    public DbSet<CsdQuestionaire> CsdQuestionaires { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=app.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure CsdQuestionaireSystem entity
        modelBuilder.Entity<CsdQuestionaireSystems>(entity =>
        {
            entity.ToTable("csd_questionaire_systems");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.QuestionaireType)
                .IsRequired()
                .HasColumnName("questionaire_type")
                .HasColumnType("varchar(1)");

            entity.Property(e => e.Code)
                .IsRequired()
                .HasColumnName("code")
                .HasColumnType("varchar(50)");

            entity.Property(e => e.CompanyId)
                .IsRequired()
                .HasColumnName("company_id")
                .HasColumnType("varchar(20)");

            entity.Property(e => e.Year)
                .IsRequired()
                .HasColumnName("year");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasColumnName("name")
                .HasColumnType("nvarchar(50)");

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.CreateUserId)
                .HasColumnName("createuserid")
                .HasColumnType("varchar(20)");

            entity.Property(e => e.CreateDt)
                .HasColumnName("createdt")
                .HasColumnType("datetime2");

            entity.Property(e => e.UpdateUserId)
                .HasColumnName("updateuserid")
                .HasColumnType("varchar(20)");

            entity.Property(e => e.UpdateDt)
                .HasColumnName("updatedt")
                .HasColumnType("datetime2");

            // Define unique constraints as per metadata
            entity.HasIndex(e => new { e.QuestionaireType, e.Code, e.CompanyId, e.Year }).IsUnique();

            // Foreign Key Relationship: csd_questionaire_systems.questionaire_type -> csd_questionaires.type
            entity.HasOne(d => d.Questionaire)
                  .WithMany(p => p.CsdQuestionaireSystems)
                  .HasForeignKey(d => d.QuestionaireType)
                  .HasPrincipalKey(p => p.Type) // Targets the 'type' column in csd_questionaires, which is an alternate key.
                  .IsRequired();
        });

        // Configure CsdQuestionaire entity
        modelBuilder.Entity<CsdQuestionaire>(entity =>
        {
            entity.ToTable("csd_questionaires");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Type)
                .IsRequired()
                .HasColumnName("type")
                .HasColumnType("varchar(1)");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasColumnName("name")
                .HasColumnType("nvarchar(50)");

            entity.Property(e => e.Url)
                .HasColumnName("url")
                .HasColumnType("nvarchar(200)");

            entity.Property(e => e.Enabled)
                .IsRequired()
                .HasColumnName("enabled");

            // Define unique constraint for 'type' as per metadata, which also serves as an alternate key for FKs
            entity.HasIndex(e => e.Type).IsUnique();
        });
    }
}