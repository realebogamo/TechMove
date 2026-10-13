using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GLMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ServiceLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SlaResponseHours = table.Column<int>(type: "int", nullable: false),
                    SignedAgreementStoredName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SignedAgreementOriginalName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.CheckConstraint("CK_Contracts_EndAfterStart", "[EndDate] > [StartDate]");
                    table.ForeignKey(
                        name: "FK_Contracts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractStatusHistory_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostZar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                    table.CheckConstraint("CK_ServiceRequests_CostUsd", "[CostUsd] > 0");
                    table.CheckConstraint("CK_ServiceRequests_ExchangeRate", "[ExchangeRate] > 0");
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Clients",
                columns: new[] { "Id", "ContactEmail", "ContactPerson", "ContactPhone", "Name", "Region" },
                values: new object[,]
                {
                    { 1, "thandi.nkosi@capefreight.example", "Thandi Nkosi", "+27 21 555 0101", "Cape Freight Holdings", "Africa" },
                    { 2, "p.devries@rotterdam-maritime.example", "Pieter de Vries", "+31 10 555 0202", "Rotterdam Maritime BV", "Europe" },
                    { 3, "mei.lin@pacificrim.example", "Mei Lin", "+65 6555 0303", "Pacific Rim Logistics", "Asia-Pacific" }
                });

            migrationBuilder.InsertData(
                table: "Contracts",
                columns: new[] { "Id", "ClientId", "CreatedAtUtc", "EndDate", "ServiceLevel", "SignedAgreementOriginalName", "SignedAgreementStoredName", "SlaResponseHours", "StartDate", "Status", "Title" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 1, 15, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Premium", null, null, 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", "Durban port container handling" },
                    { 2, 2, new DateTime(2026, 1, 15, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 5, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Express", null, null, 24, new DateTime(2025, 6, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "OnHold", "EU cross-dock distribution" },
                    { 3, 3, new DateTime(2026, 1, 15, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Standard", null, null, 72, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Expired", "Singapore air freight 2025" },
                    { 4, 1, new DateTime(2026, 1, 15, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2028, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Standard", null, null, 72, new DateTime(2027, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Draft", "Johannesburg warehousing renewal" }
                });

            migrationBuilder.InsertData(
                table: "ServiceRequests",
                columns: new[] { "Id", "ContractId", "CostUsd", "CostZar", "CreatedAtUtc", "Description", "ExchangeRate", "Status" },
                values: new object[] { 1, 1, 1200m, 21900m, new DateTime(2026, 1, 15, 8, 0, 0, 0, DateTimeKind.Utc), "Additional reefer container inspection", 18.25m, "Approved" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ClientId",
                table: "Contracts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_StartDate",
                table: "Contracts",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status_EndDate",
                table: "Contracts",
                columns: new[] { "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractStatusHistory_ContractId",
                table: "ContractStatusHistory",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ContractId",
                table: "ServiceRequests",
                column: "ContractId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractStatusHistory");

            migrationBuilder.DropTable(
                name: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
