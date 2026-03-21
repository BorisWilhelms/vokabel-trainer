using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VokabelTrainer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddResponseSecondsToAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ResponseSeconds",
                table: "TrainingAnswers",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponseSeconds",
                table: "TrainingAnswers");
        }
    }
}
