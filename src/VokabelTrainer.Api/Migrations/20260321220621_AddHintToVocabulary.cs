using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VokabelTrainer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHintToVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hint",
                table: "Vocabularies",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hint",
                table: "Vocabularies");
        }
    }
}
