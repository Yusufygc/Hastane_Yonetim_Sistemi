using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospital.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_patient_identity_number",
                table: "patient");

            migrationBuilder.DropIndex(
                name: "ix_doctor_service_status_created_at",
                table: "doctor_service");

            migrationBuilder.AlterColumn<string>(
                name: "identity_number",
                table: "patient",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(11)",
                oldFixedLength: true,
                oldMaxLength: 11);

            migrationBuilder.AddColumn<string>(
                name: "identity_number_hash",
                table: "patient",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_identity_number_hash",
                table: "patient",
                column: "identity_number_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doctor_service_pending",
                table: "doctor_service",
                column: "created_at",
                filter: "status = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_pending_created_at",
                table: "appointment",
                column: "created_at",
                filter: "status = 'PENDING'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DİKKAT: Geri alma şifreli T.C. kimlik numaralarını (enc:v1:..., 59 karakter) char(11) sütununa sığdıramaz ve
            // düz metne çeviremez. Şifreleme yükseltmesi çalıştıktan sonra bu migration geri alınmamalıdır; gerekirse önce
            // veritabanı yedeğinden dönün.
            migrationBuilder.DropIndex(
                name: "ix_patient_identity_number_hash",
                table: "patient");

            migrationBuilder.DropIndex(
                name: "ix_doctor_service_pending",
                table: "doctor_service");

            migrationBuilder.DropIndex(
                name: "ix_appointment_pending_created_at",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "identity_number_hash",
                table: "patient");

            migrationBuilder.AlterColumn<string>(
                name: "identity_number",
                table: "patient",
                type: "character(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "ix_patient_identity_number",
                table: "patient",
                column: "identity_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doctor_service_status_created_at",
                table: "doctor_service",
                columns: new[] { "status", "created_at" });
        }
    }
}
