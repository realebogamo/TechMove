using GLMS.Core.Patterns.Factory;
using GLMS.Core.Patterns.Observer;
using GLMS.Core.Patterns.State;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GLMS.UnitTests;

public class ContractStateTests
{
    [Theory]
    [InlineData(ContractStatus.Active, true)]
    [InlineData(ContractStatus.Draft, false)]
    [InlineData(ContractStatus.OnHold, false)]
    [InlineData(ContractStatus.Expired, false)]
    public void OnlyActiveState_AcceptsServiceRequests(ContractStatus status, bool expected)
    {
        Assert.Equal(expected, ContractStates.For(status).CanAcceptServiceRequests);
    }

    [Theory]
    [InlineData(ContractStatus.Draft, ContractStatus.Active, true)]
    [InlineData(ContractStatus.Draft, ContractStatus.OnHold, false)]
    [InlineData(ContractStatus.Active, ContractStatus.OnHold, true)]
    [InlineData(ContractStatus.Active, ContractStatus.Expired, true)]
    [InlineData(ContractStatus.Active, ContractStatus.Draft, false)]
    [InlineData(ContractStatus.OnHold, ContractStatus.Active, true)]
    [InlineData(ContractStatus.Expired, ContractStatus.Active, false)]
    public void Transitions_FollowContractLifecycle(ContractStatus from, ContractStatus to, bool allowed)
    {
        Assert.Equal(allowed, ContractStates.For(from).CanTransitionTo(to));
    }

    [Fact]
    public void ExpiredState_IsTerminal_AndNotEditable()
    {
        var state = ContractStates.For(ContractStatus.Expired);
        Assert.Empty(state.AllowedTransitions);
        Assert.False(state.CanEditTerms);
    }
}

public class ContractFactoryTests
{
    private readonly ContractFactory _factory = new(
        new ContractCreator[] { new StandardContractCreator(), new ExpressContractCreator(), new PremiumContractCreator() },
        new FixedTimeProvider(TestData.Today));

    [Theory]
    [InlineData(ServiceLevel.Standard, 72)]
    [InlineData(ServiceLevel.Express, 24)]
    [InlineData(ServiceLevel.Premium, 4)]
    public void Create_UsesCreatorForServiceLevel(ServiceLevel level, int expectedSlaHours)
    {
        var contract = _factory.Create(new CreateContractDto
        {
            Title = "  Durban port  ", ClientId = 3, ServiceLevel = level,
            StartDate = TestData.Today, EndDate = TestData.Today.AddYears(1)
        });

        Assert.Equal(level, contract.ServiceLevel);
        Assert.Equal(expectedSlaHours, contract.SlaResponseHours);
        Assert.Equal(ContractStatus.Draft, contract.Status);
        Assert.Equal("Durban port", contract.Title);
        Assert.Equal(3, contract.ClientId);
    }

    [Fact]
    public void Create_UnregisteredServiceLevel_Throws()
    {
        var factory = new ContractFactory(new ContractCreator[] { new StandardContractCreator() }, TimeProvider.System);
        Assert.Throws<NotSupportedException>(() => factory.Create(new CreateContractDto { ServiceLevel = ServiceLevel.Premium }));
    }
}

public class ContractStatusPublisherTests
{
    private static readonly ContractStatusChangedEvent Event =
        new(1, "Contract 1", ContractStatus.Active, ContractStatus.OnHold, "test", TestData.Today);

    [Fact]
    public async Task Publish_NotifiesEveryObserver()
    {
        var first = new Mock<IContractStatusObserver>();
        var second = new Mock<IContractStatusObserver>();
        var publisher = new ContractStatusPublisher(new[] { first.Object, second.Object }, NullLogger<ContractStatusPublisher>.Instance);

        await publisher.PublishAsync(Event);

        first.Verify(o => o.OnStatusChangedAsync(Event, It.IsAny<CancellationToken>()), Times.Once);
        second.Verify(o => o.OnStatusChangedAsync(Event, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_FailingObserver_DoesNotStopOthers()
    {
        var failing = new Mock<IContractStatusObserver>();
        failing.Setup(o => o.OnStatusChangedAsync(It.IsAny<ContractStatusChangedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var healthy = new Mock<IContractStatusObserver>();
        var publisher = new ContractStatusPublisher(new[] { failing.Object, healthy.Object }, NullLogger<ContractStatusPublisher>.Instance);

        await publisher.PublishAsync(Event);

        healthy.Verify(o => o.OnStatusChangedAsync(Event, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubscribeAndUnsubscribe_ControlWhoIsNotified()
    {
        var observer = new Mock<IContractStatusObserver>();
        var publisher = new ContractStatusPublisher(Array.Empty<IContractStatusObserver>(), NullLogger<ContractStatusPublisher>.Instance);

        publisher.Subscribe(observer.Object);
        await publisher.PublishAsync(Event);
        publisher.Unsubscribe(observer.Object);
        await publisher.PublishAsync(Event);

        observer.Verify(o => o.OnStatusChangedAsync(Event, It.IsAny<CancellationToken>()), Times.Once);
    }
}
