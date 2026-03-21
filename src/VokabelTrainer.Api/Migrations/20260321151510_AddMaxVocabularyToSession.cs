using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VokabelTrainer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxVocabularyToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxVocabulary",
                table: "TrainingSessions",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxVocabulary",
                table: "TrainingSessions");
        }
    }
}
