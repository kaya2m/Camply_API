using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camply.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMlTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "BirthDate",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateTable(
                name: "ml_content_features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FeatureVector = table.Column<string>(type: "jsonb", nullable: false),
                    FeatureCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Categories = table.Column<string>(type: "jsonb", nullable: true),
                    QualityScore = table.Column<float>(type: "real", nullable: false),
                    ViralPotential = table.Column<float>(type: "real", nullable: true),
                    SentimentScore = table.Column<float>(type: "real", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_content_features", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ml_experiments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ExperimentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ControlConfig = table.Column<string>(type: "jsonb", nullable: true),
                    TestConfig = table.Column<string>(type: "jsonb", nullable: true),
                    TrafficPercentage = table.Column<int>(type: "integer", nullable: false),
                    TargetMetrics = table.Column<string>(type: "jsonb", nullable: true),
                    Results = table.Column<string>(type: "jsonb", nullable: true),
                    StatisticalSignificance = table.Column<float>(type: "real", nullable: true),
                    Winner = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_experiments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ml_models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModelPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TrainedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrainingDataInfo = table.Column<string>(type: "jsonb", nullable: true),
                    PerformanceMetrics = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Hyperparameters = table.Column<string>(type: "jsonb", nullable: true),
                    ModelSize = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ml_user_features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureVector = table.Column<string>(type: "jsonb", nullable: false),
                    FeatureType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastCalculated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QualityScore = table.Column<float>(type: "real", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_user_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ml_user_features_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ml_user_experiments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_user_experiments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ml_user_experiments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ml_user_experiments_ml_experiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "ml_experiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ml_training_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: true),
                    Results = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    LogPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_training_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ml_training_jobs_ml_models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "ml_models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ml_content_features_content_category",
                table: "ml_content_features",
                columns: new[] { "ContentId", "ContentType", "FeatureCategory" });

            migrationBuilder.CreateIndex(
                name: "ix_ml_content_features_quality",
                table: "ml_content_features",
                column: "QualityScore");

            migrationBuilder.CreateIndex(
                name: "ix_ml_content_features_viral",
                table: "ml_content_features",
                column: "ViralPotential");

            migrationBuilder.CreateIndex(
                name: "ix_ml_experiments_date_range",
                table: "ml_experiments",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "ix_ml_experiments_status",
                table: "ml_experiments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_ml_experiments_type",
                table: "ml_experiments",
                column: "ExperimentType");

            migrationBuilder.CreateIndex(
                name: "ix_ml_models_name_version",
                table: "ml_models",
                columns: new[] { "Name", "Version" });

            migrationBuilder.CreateIndex(
                name: "ix_ml_models_trained_at",
                table: "ml_models",
                column: "TrainedAt");

            migrationBuilder.CreateIndex(
                name: "ix_ml_models_type_active",
                table: "ml_models",
                columns: new[] { "ModelType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_ml_training_jobs_model",
                table: "ml_training_jobs",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "ix_ml_training_jobs_started_at",
                table: "ml_training_jobs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "ix_ml_training_jobs_status",
                table: "ml_training_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ml_user_experiments_ExperimentId",
                table: "ml_user_experiments",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "ix_ml_user_experiments_group",
                table: "ml_user_experiments",
                column: "AssignedGroup");

            migrationBuilder.CreateIndex(
                name: "ix_ml_user_experiments_user_experiment",
                table: "ml_user_experiments",
                columns: new[] { "UserId", "ExperimentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ml_user_features_last_calculated",
                table: "ml_user_features",
                column: "LastCalculated");

            migrationBuilder.CreateIndex(
                name: "ix_ml_user_features_quality",
                table: "ml_user_features",
                column: "QualityScore");

            migrationBuilder.CreateIndex(
                name: "ix_ml_user_features_user_type_version",
                table: "ml_user_features",
                columns: new[] { "UserId", "FeatureType", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ml_content_features");

            migrationBuilder.DropTable(
                name: "ml_training_jobs");

            migrationBuilder.DropTable(
                name: "ml_user_experiments");

            migrationBuilder.DropTable(
                name: "ml_user_features");

            migrationBuilder.DropTable(
                name: "ml_models");

            migrationBuilder.DropTable(
                name: "ml_experiments");

            migrationBuilder.AlterColumn<DateTime>(
                name: "BirthDate",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
