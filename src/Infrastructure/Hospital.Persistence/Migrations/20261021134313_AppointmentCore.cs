using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospital.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Faz 1B spike satırları (doktor/hizmet/şube bilgisi olmayan sahte kimlikler) yeni FK'lerle uyumsuzdur;
            // gerçek randevu verisi bu migration'dan önce oluşamaz (randevu komutları Faz 4'tür).
            migrationBuilder.Sql("DELETE FROM appointment;");

            migrationBuilder.AddColumn<string>(
                name: "currency_snapshot",
                table: "appointment",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "decision_reason",
                table: "appointment",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration_minutes_snapshot",
                table: "appointment",
                type: "integer",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "hospital_id",
                table: "appointment",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "patient_note",
                table: "appointment",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price_snapshot",
                table: "appointment",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "service_id",
                table: "appointment",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "service_name_snapshot",
                table: "appointment",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "status_changed_by",
                table: "appointment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "appointment",
                type: "timestamp with time zone",
                nullable: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "appointment",
                type: "xid",
                rowVersion: true,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ix_appointment_hospital_id_status_start_at",
                table: "appointment",
                columns: new[] { "hospital_id", "status", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_service_id",
                table: "appointment",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_status_changed_by",
                table: "appointment",
                column: "status_changed_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appointment_snapshot",
                table: "appointment",
                sql: "duration_minutes_snapshot > 0 AND price_snapshot >= 0");

            migrationBuilder.AddForeignKey(
                name: "fk_appointment_doctors_doctor_id",
                table: "appointment",
                column: "doctor_id",
                principalTable: "doctor",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_appointment_hospitals_hospital_id",
                table: "appointment",
                column: "hospital_id",
                principalTable: "hospital",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_appointment_services_service_id",
                table: "appointment",
                column: "service_id",
                principalTable: "service",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_appointment_users_status_changed_by",
                table: "appointment",
                column: "status_changed_by",
                principalTable: "user_account",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_appointment_doctors_doctor_id",
                table: "appointment");

            migrationBuilder.DropForeignKey(
                name: "fk_appointment_hospitals_hospital_id",
                table: "appointment");

            migrationBuilder.DropForeignKey(
                name: "fk_appointment_services_service_id",
                table: "appointment");

            migrationBuilder.DropForeignKey(
                name: "fk_appointment_users_status_changed_by",
                table: "appointment");

            migrationBuilder.DropIndex(
                name: "ix_appointment_hospital_id_status_start_at",
                table: "appointment");

            migrationBuilder.DropIndex(
                name: "ix_appointment_service_id",
                table: "appointment");

            migrationBuilder.DropIndex(
                name: "ix_appointment_status_changed_by",
                table: "appointment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_appointment_snapshot",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "currency_snapshot",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "decision_reason",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "duration_minutes_snapshot",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "hospital_id",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "patient_note",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "price_snapshot",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "service_id",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "service_name_snapshot",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "status_changed_by",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "appointment");
        }
    }
}
