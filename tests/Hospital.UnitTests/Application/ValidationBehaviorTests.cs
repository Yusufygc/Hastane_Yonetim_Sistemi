using AwesomeAssertions;
using FluentValidation;
using Hospital.Application.Common;
using Hospital.Application.Common.Behaviors;
using Mediator;

namespace Hospital.UnitTests.Application;

public class ValidationBehaviorTests
{
    private sealed record SampleCommand(string Name) : ICommand<Result<int>>;

    private sealed class SampleValidator : AbstractValidator<SampleCommand>
    {
        public SampleValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Ad zorunludur.");
    }

    [Fact]
    public async Task Gecersiz_mesaj_handlera_girmeden_dogrulama_hatasi_doner()
    {
        var behavior = new ValidationBehavior<SampleCommand, Result<int>>([new SampleValidator()]);
        var handlerCalled = false;

        var result = await behavior.Handle(
            new SampleCommand(""),
            (_, _) =>
            {
                handlerCalled = true;
                return ValueTask.FromResult<Result<int>>(1);
            },
            default);

        handlerCalled.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Details.Should().ContainKey(nameof(SampleCommand.Name));
    }

    [Fact]
    public async Task Gecerli_mesaj_handlera_iletilir()
    {
        var behavior = new ValidationBehavior<SampleCommand, Result<int>>([new SampleValidator()]);

        var result = await behavior.Handle(
            new SampleCommand("Ayşe"),
            (_, _) => ValueTask.FromResult<Result<int>>(7),
            default);

        result.Value.Should().Be(7);
    }

    [Fact]
    public async Task Validator_yoksa_mesaj_dogrudan_handlera_iletilir()
    {
        var behavior = new ValidationBehavior<SampleCommand, Result<int>>([]);

        var result = await behavior.Handle(
            new SampleCommand(""),
            (_, _) => ValueTask.FromResult<Result<int>>(3),
            default);

        result.Value.Should().Be(3);
    }
}
