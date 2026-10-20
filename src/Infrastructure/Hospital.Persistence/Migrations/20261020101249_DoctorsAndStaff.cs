using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospital.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DoctorsAndStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doctor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    diploma_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor", x => x.id);
                    table.ForeignKey(
                        name: "fk_doctor_department_department_id",
                        column: x => x.department_id,
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doctor_user_account_id",
                        column: x => x.id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    hospital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_department_department_id",
                        column: x => x.department_id,
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_hospital_hospital_id",
                        column: x => x.hospital_id,
                        principalTable: "hospital",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_user_account_id",
                        column: x => x.id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "doctor_availability",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<short>(type: "smallint", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_availability", x => x.id);
                    table.CheckConstraint("ck_doctor_availability_day", "day_of_week BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_doctor_availability_range", "end_time > start_time");
                    table.CheckConstraint("ck_doctor_availability_validity", "valid_to IS NULL OR valid_to >= valid_from");
                    table.ForeignKey(
                        name: "fk_doctor_availability_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "doctor_service",
                columns: table => new
                {
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_service", x => new { x.doctor_id, x.service_id });
                    table.CheckConstraint("ck_doctor_service_status", "status IN ('PENDING', 'APPROVED', 'REJECTED')");
                    table.ForeignKey(
                        name: "fk_doctor_service_doctor_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_doctor_service_services_service_id",
                        column: x => x.service_id,
                        principalTable: "service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doctor_service_user_account_decided_by",
                        column: x => x.decided_by,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doctor_service_user_account_requested_by",
                        column: x => x.requested_by,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doctor_time_off",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_time_off", x => x.id);
                    table.CheckConstraint("ck_doctor_time_off_range", "end_at > start_at");
                    table.ForeignKey(
                        name: "fk_doctor_time_off_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_doctor_department_id",
                table: "doctor",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_doctor_diploma_no",
                table: "doctor",
                column: "diploma_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doctor_availability_doctor_id_day_of_week",
                table: "doctor_availability",
                columns: new[] { "doctor_id", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "ix_doctor_service_decided_by",
                table: "doctor_service",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_doctor_service_requested_by",
                table: "doctor_service",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_doctor_service_service_id",
                table: "doctor_service",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_doctor_service_status_created_at",
                table: "doctor_service",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_doctor_time_off_doctor_id_start_at",
                table: "doctor_time_off",
                columns: new[] { "doctor_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_department_id",
                table: "staff",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_hospital_id",
                table: "staff",
                column: "hospital_id");

            // Eşzamanlı isteklerde de çakışmayı veritabanı engeller (asıl güvence; uygulama kontrolü ön doğrulamadır).
            // Uygunluk: aynı doktorun aynı günkü pencereleri, hem saat aralığı hem geçerlilik dönemi kesişiyorsa çakışır.
            // valid_to boşsa süresiz (daterange üst sınırı NULL = sınırsız). Saat aralığı sabit bir günün üstüne taşınarak tsrange'e çevrilir.
            migrationBuilder.Sql("""
                ALTER TABLE doctor_availability
                  ADD CONSTRAINT ex_doctor_availability_no_overlap
                  EXCLUDE USING gist (
                    doctor_id WITH =,
                    day_of_week WITH =,
                    daterange(valid_from, valid_to, '[]') WITH &&,
                    tsrange(DATE '2000-01-01' + start_time, DATE '2000-01-01' + end_time, '[)') WITH &&
                  );
                """);

            // İzin: aynı doktorun izin aralıkları kesişemez ([) yarı açık aralık: bitişik izinler çakışmaz).
            migrationBuilder.Sql("""
                ALTER TABLE doctor_time_off
                  ADD CONSTRAINT ex_doctor_time_off_no_overlap
                  EXCLUDE USING gist (
                    doctor_id WITH =,
                    tstzrange(start_at, end_at, '[)') WITH &&
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_availability");

            migrationBuilder.DropTable(
                name: "doctor_service");

            migrationBuilder.DropTable(
                name: "doctor_time_off");

            migrationBuilder.DropTable(
                name: "staff");

            migrationBuilder.DropTable(
                name: "doctor");
        }
    }
}
