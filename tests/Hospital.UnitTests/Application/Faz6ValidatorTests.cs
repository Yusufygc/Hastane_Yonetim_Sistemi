using AwesomeAssertions;
using Hospital.Application.Features.Appointments.Queries.GetDoctorAppointments;
using Hospital.Application.Features.Appointments.Queries.GetMyAppointments;
using Hospital.Application.Features.Appointments.Queries.ListAppointments;
using Hospital.Application.Features.Doctors.Queries.ListDoctors;
using Hospital.Application.Features.Doctors.Queries.ListDoctorServiceRequests;
using Hospital.Application.Features.StaffMembers.Queries.ListStaff;
using Hospital.Application.Parameters;

namespace Hospital.UnitTests.Application;

public class Faz6ValidatorTests
{
    public static TheoryData<int?, int?, bool> Pages => new()
    {
        { null, null, true },
        { 1, 1, true },
        { 3, 50, true },
        { 1, PageRequest.MaxPageSize, true },
        { 0, 10, false },
        { -1, 10, false },
        { 1, 0, false },
        { 1, -5, false },
        { 1, PageRequest.MaxPageSize + 1, false },
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public void Tum_liste_sorgularinda_sayfalama_dogrulanir(int? page, int? pageSize, bool valid)
    {
        new GetMyAppointmentsValidator().Validate(new GetMyAppointmentsQuery(null, page, pageSize)).IsValid.Should().Be(valid);
        new GetDoctorAppointmentsValidator().Validate(new GetDoctorAppointmentsQuery(null, null, null, page, pageSize)).IsValid.Should().Be(valid);
        new ListAppointmentsValidator().Validate(new ListAppointmentsQuery(null, null, null, null, page, pageSize)).IsValid.Should().Be(valid);
        new ListDoctorsValidator().Validate(new ListDoctorsQuery(null, null, page, pageSize)).IsValid.Should().Be(valid);
        new ListDoctorServiceRequestsValidator().Validate(new ListDoctorServiceRequestsQuery(null, page, pageSize)).IsValid.Should().Be(valid);
        new ListStaffValidator().Validate(new ListStaffQuery(null, page, pageSize)).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Hata_alan_adi_page_ve_pageSize_olarak_doner()
    {
        var result = new ListDoctorsValidator().Validate(new ListDoctorsQuery(null, null, 0, 500));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo([nameof(ListDoctorsQuery.Page), nameof(ListDoctorsQuery.PageSize)]);
    }

    [Fact]
    public void PageRequest_varsayilanlar_ve_atlama_hesabi()
    {
        PageRequest.From(null, null).Should().Be(new PageRequest(1, PageRequest.DefaultPageSize));
        PageRequest.From(3, 20).Skip.Should().Be(40);
        PageRequest.From(1, 20).Skip.Should().Be(0);
        PageRequest.Largest.PageSize.Should().Be(PageRequest.MaxPageSize);
    }

    [Fact]
    public void PagedList_Map_toplam_ve_sayfa_bilgisini_korur()
    {
        var page = new PagedList<int>([1, 2, 3], 10, new PageRequest(2, 3));

        var mapped = page.Map(x => x.ToString());

        mapped.Items.Should().Equal("1", "2", "3");
        mapped.TotalCount.Should().Be(10);
        mapped.Page.Should().Be(new PageRequest(2, 3));
    }
}
