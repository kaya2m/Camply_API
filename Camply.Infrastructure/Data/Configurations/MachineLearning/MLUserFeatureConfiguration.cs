using Camply.Domain.MachineLearning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Data.Configurations.MachineLearning
{
    public class MLUserFeatureConfiguration : IEntityTypeConfiguration<MLUserFeature>
    {
        public void Configure(EntityTypeBuilder<MLUserFeature> builder)
        {
            builder.ToTable("ml_user_features");

            builder.HasKey(uf => uf.Id);

            builder.Property(uf => uf.FeatureVector)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(uf => uf.FeatureType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(uf => uf.Version)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(uf => uf.Metadata)
                .HasColumnType("jsonb");

            // User relationship
            builder.HasOne(uf => uf.User)
                .WithMany()
                .HasForeignKey(uf => uf.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(uf => new { uf.UserId, uf.FeatureType, uf.Version })
                .HasDatabaseName("ix_ml_user_features_user_type_version");

            builder.HasIndex(uf => uf.LastCalculated)
                .HasDatabaseName("ix_ml_user_features_last_calculated");

            builder.HasIndex(uf => uf.QualityScore)
                .HasDatabaseName("ix_ml_user_features_quality");
        }
    }

    public class MLContentFeatureConfiguration : IEntityTypeConfiguration<MLContentFeature>
    {
        public void Configure(EntityTypeBuilder<MLContentFeature> builder)
        {
            builder.ToTable("ml_content_features");

            builder.HasKey(cf => cf.Id);

            builder.Property(cf => cf.FeatureVector)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(cf => cf.ContentType)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(cf => cf.FeatureCategory)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(cf => cf.Version)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(cf => cf.Categories)
                .HasColumnType("jsonb");

            builder.Property(cf => cf.Metadata)
                .HasColumnType("jsonb");

            // Indexes
            builder.HasIndex(cf => new { cf.ContentId, cf.ContentType, cf.FeatureCategory })
                .HasDatabaseName("ix_ml_content_features_content_category");

            builder.HasIndex(cf => cf.QualityScore)
                .HasDatabaseName("ix_ml_content_features_quality");

            builder.HasIndex(cf => cf.ViralPotential)
                .HasDatabaseName("ix_ml_content_features_viral");
        }
    }

    public class MLModelConfiguration : IEntityTypeConfiguration<MLModel>
    {
        public void Configure(EntityTypeBuilder<MLModel> builder)
        {
            builder.ToTable("ml_models");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(m => m.Version)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(m => m.ModelType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(m => m.ModelPath)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(m => m.Status)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(m => m.PerformanceMetrics)
                .HasColumnType("jsonb");

            builder.Property(m => m.Hyperparameters)
                .HasColumnType("jsonb");

            builder.Property(m => m.TrainingDataInfo)
                .HasColumnType("jsonb");

            // Indexes
            builder.HasIndex(m => new { m.Name, m.Version })
                .HasDatabaseName("ix_ml_models_name_version");

            builder.HasIndex(m => new { m.ModelType, m.IsActive })
                .HasDatabaseName("ix_ml_models_type_active");

            builder.HasIndex(m => m.TrainedAt)
                .HasDatabaseName("ix_ml_models_trained_at");
        }
    }

    public class MLTrainingJobConfiguration : IEntityTypeConfiguration<MLTrainingJob>
    {
        public void Configure(EntityTypeBuilder<MLTrainingJob> builder)
        {
            builder.ToTable("ml_training_jobs");

            builder.HasKey(tj => tj.Id);

            builder.Property(tj => tj.JobName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(tj => tj.Status)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(tj => tj.Parameters)
                .HasColumnType("jsonb");

            builder.Property(tj => tj.Results)
                .HasColumnType("jsonb");

            builder.Property(tj => tj.ErrorMessage)
                .HasColumnType("text");

            builder.Property(tj => tj.LogPath)
                .HasMaxLength(500);

            // Model relationship
            builder.HasOne(tj => tj.Model)
                .WithMany()
                .HasForeignKey(tj => tj.ModelId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            builder.HasIndex(tj => tj.Status)
                .HasDatabaseName("ix_ml_training_jobs_status");

            builder.HasIndex(tj => tj.StartedAt)
                .HasDatabaseName("ix_ml_training_jobs_started_at");

            builder.HasIndex(tj => tj.ModelId)
                .HasDatabaseName("ix_ml_training_jobs_model");
        }
    }

    public class MLExperimentConfiguration : IEntityTypeConfiguration<MLExperiment>
    {
        public void Configure(EntityTypeBuilder<MLExperiment> builder)
        {
            builder.ToTable("ml_experiments");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.ExperimentType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.ControlConfig)
                .HasColumnType("jsonb");

            builder.Property(e => e.TestConfig)
                .HasColumnType("jsonb");

            builder.Property(e => e.TargetMetrics)
                .HasColumnType("jsonb");

            builder.Property(e => e.Results)
                .HasColumnType("jsonb");

            builder.Property(e => e.Winner)
                .HasMaxLength(50);

            // Indexes
            builder.HasIndex(e => e.Status)
                .HasDatabaseName("ix_ml_experiments_status");

            builder.HasIndex(e => new { e.StartDate, e.EndDate })
                .HasDatabaseName("ix_ml_experiments_date_range");

            builder.HasIndex(e => e.ExperimentType)
                .HasDatabaseName("ix_ml_experiments_type");
        }
    }

    public class MLUserExperimentConfiguration : IEntityTypeConfiguration<MLUserExperiment>
    {
        public void Configure(EntityTypeBuilder<MLUserExperiment> builder)
        {
            builder.ToTable("ml_user_experiments");

            builder.HasKey(ue => ue.Id);

            builder.Property(ue => ue.AssignedGroup)
                .IsRequired()
                .HasMaxLength(50);

            // User relationship
            builder.HasOne(ue => ue.User)
                .WithMany()
                .HasForeignKey(ue => ue.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Experiment relationship
            builder.HasOne(ue => ue.Experiment)
                .WithMany()
                .HasForeignKey(ue => ue.ExperimentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint
            builder.HasIndex(ue => new { ue.UserId, ue.ExperimentId })
                .IsUnique()
                .HasDatabaseName("ix_ml_user_experiments_user_experiment");

            builder.HasIndex(ue => ue.AssignedGroup)
                .HasDatabaseName("ix_ml_user_experiments_group");
        }
    }
}
