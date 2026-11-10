using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceLens.Api.Migrations
{
    /// <inheritdoc />
    public partial class RetireUnusedFeedbackModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retire EF mappings only. Existing unused tables and their data must remain intact.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The tables were never dropped, so rollback must not recreate or overwrite them.
        }
    }
}
