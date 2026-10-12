using AwesomeAssertions;
using Hospital.Application.Common;

namespace Hospital.UnitTests.Application;

public class ResultTests
{
    [Fact]
    public void Basarili_sonuc_deger_tasir()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Basarisiz_sonucun_degeri_okunamaz()
    {
        Result<int> result = Error.NotFound("x.not_found", "Yok");

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IFailureFactory_ile_genel_tipten_basarisiz_sonuc_uretilir()
    {
        var error = Error.Conflict("c", "m");

        Create<Result<Guid>>(error).Error.Should().Be(error);
        Create<Result>(error).IsFailure.Should().BeTrue();
    }

    private static T Create<T>(Error error)
        where T : IFailureFactory<T> => T.Failure(error);
}
