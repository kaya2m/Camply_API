using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camply.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationFeautre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "LocationReviews");

            migrationBuilder.RenameColumn(
                name: "Amenities",
                table: "Locations",
                newName: "RejectionReason");

            migrationBuilder.AddColumn<Guid>(
                name: "LocationReviewId",
                table: "Media",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Website",
                table: "Locations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "Locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PostalCode",
                table: "Locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OpeningHours",
                table: "Locations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Locations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContactPhone",
                table: "Locations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Locations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Locations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EntryFee",
                table: "Locations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Features",
                table: "Locations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "HasEntryFee",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSponsored",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxCapacity",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxVehicles",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SponsoredPriority",
                table: "Locations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SponsoredUntil",
                table: "Locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalVisitCount",
                table: "Locations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TwitterUrl",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "LocationReviews",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "LocationReviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CleanlinessRating",
                table: "LocationReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FacilitiesRating",
                table: "LocationReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HelpfulCount",
                table: "LocationReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecommended",
                table: "LocationReviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LocationRating",
                table: "LocationReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NotHelpfulCount",
                table: "LocationReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OverallRating",
                table: "LocationReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OwnerResponse",
                table: "LocationReviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerResponseDate",
                table: "LocationReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceRating",
                table: "LocationReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StayDuration",
                table: "LocationReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValueRating",
                table: "LocationReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VisitDate",
                table: "LocationReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocationBookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationBookmarks_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocationBookmarks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewHelpfuls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsHelpful = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewHelpfuls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewHelpfuls_LocationReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "LocationReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewHelpfuls_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Media_LocationReviewId",
                table: "Media",
                column: "LocationReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ApprovedByUserId",
                table: "Locations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationBookmarks_LocationId",
                table: "LocationBookmarks",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationBookmarks_UserId",
                table: "LocationBookmarks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHelpfuls_ReviewId",
                table: "ReviewHelpfuls",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHelpfuls_UserId",
                table: "ReviewHelpfuls",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Users_ApprovedByUserId",
                table: "Locations",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_LocationReviews_LocationReviewId",
                table: "Media",
                column: "LocationReviewId",
                principalTable: "LocationReviews",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Users_ApprovedByUserId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_LocationReviews_LocationReviewId",
                table: "Media");

            migrationBuilder.DropTable(
                name: "LocationBookmarks");

            migrationBuilder.DropTable(
                name: "ReviewHelpfuls");

            migrationBuilder.DropIndex(
                name: "IX_Media_LocationReviewId",
                table: "Media");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ApprovedByUserId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "LocationReviewId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "EntryFee",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Features",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "HasEntryFee",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IsSponsored",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "MaxCapacity",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "MaxVehicles",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SponsoredPriority",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SponsoredUntil",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TotalVisitCount",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TwitterUrl",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CleanlinessRating",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "FacilitiesRating",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "HelpfulCount",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "IsRecommended",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "LocationRating",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "NotHelpfulCount",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "OverallRating",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "OwnerResponse",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "OwnerResponseDate",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "ServiceRating",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "StayDuration",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "ValueRating",
                table: "LocationReviews");

            migrationBuilder.DropColumn(
                name: "VisitDate",
                table: "LocationReviews");

            migrationBuilder.RenameColumn(
                name: "RejectionReason",
                table: "Locations",
                newName: "Amenities");

            migrationBuilder.AlterColumn<string>(
                name: "Website",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PostalCode",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OpeningHours",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ContactPhone",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Locations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "LocationReviews",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "LocationReviews",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "LocationReviews",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "ml_content_features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Categories = table.Column<string>(type: "jsonb", nullable: true),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    FeatureCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FeatureVector = table.Column<string>(type: "jsonb", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    QualityScore = table.Column<float>(type: "real", nullable: false),
                    SentimentScore = table.Column<float>(type: "real", nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ViralPotential = table.Column<float>(type: "real", nullable: true)
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
                    ControlConfig = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExperimentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Results = table.Column<string>(type: "jsonb", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StatisticalSignificance = table.Column<float>(type: "real", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetMetrics = table.Column<string>(type: "jsonb", nullable: true),
                    TestConfig = table.Column<string>(type: "jsonb", nullable: true),
                    TrafficPercentage = table.Column<int>(type: "integer", nullable: false),
                    Winner = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Hyperparameters = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ModelSize = table.Column<long>(type: "bigint", nullable: false),
                    ModelType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PerformanceMetrics = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TrainedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrainingDataInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    FeatureType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FeatureVector = table.Column<string>(type: "jsonb", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastCalculated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    QualityScore = table.Column<float>(type: "real", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    ModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    JobName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LogPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: true),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    Results = table.Column<string>(type: "jsonb", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
    }
}
