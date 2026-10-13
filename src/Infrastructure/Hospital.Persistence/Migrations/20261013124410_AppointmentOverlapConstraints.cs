using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospital.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentOverlapConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment", x => x.id);
                    table.CheckConstraint("ck_appointment_range", "end_at > start_at");
                    table.CheckConstraint("ck_appointment_status", "status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED', 'COMPLETED')");
                    table.ForeignKey(
                        name: "fk_appointment_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_doctor_id_start_at",
                table: "appointment",
                columns: new[] { "doctor_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_patient_id_start_at",
                table: "appointment",
                columns: new[] { "patient_id", "start_at" },
                descending: new[] { false, true });

            // Çakışma koruması veritabanı seviyesinde (asıl güvence): aynı doktorun ve aynı hastanın
            // PENDING/APPROVED randevuları zaman aralığı olarak kesişemez. [) yarı açık aralık: bitişik randevular çakışmaz.
            // CANCELLED/REJECTED/COMPLETED kısıt dışıdır (slot serbest kalır). btree_gist eklentisi ilk migration'da açılır.
            migrationBuilder.Sql("""
                ALTER TABLE appointment
                  ADD CONSTRAINT ex_doctor_no_overlap
                  EXCLUDE USING gist (
                    doctor_id WITH =,
                    tstzrange(start_at, end_at, '[)') WITH &&
                  ) WHERE (status IN ('PENDING', 'APPROVED'));
                """);

            migrationBuilder.Sql("""
                ALTER TABLE appointment
                  ADD CONSTRAINT ex_patient_no_overlap
                  EXCLUDE USING gist (
                    patient_id WITH =,
                    tstzrange(start_at, end_at, '[)') WITH &&
                  ) WHERE (status IN ('PENDING', 'APPROVED'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment");
        }
    }
}
