using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OnlineEbookShopV1.Api.Messaging;

namespace OnlineEbookShopV1.Tests.Messaging;

public class RabbitMqEventPublisherTests
{
    [Fact]
    public async Task Disabled_publisher_does_nothing()
    {
        await using var sut = new RabbitMqEventPublisher(Options.Create(new RabbitMqOptions { Enabled = false, Host = "does-not-exist" }), NullLogger<RabbitMqEventPublisher>.Instance);

        await sut.PublishAsync(DomainEvents.Create(EventTypes.Logout, "User", Guid.NewGuid(), null));
    }

    [Fact]
    public async Task Unreachable_broker_is_logged_not_thrown()
    {
        await using var sut = new RabbitMqEventPublisher(Options.Create(new RabbitMqOptions { Enabled = true, Host = "127.0.0.1", Port = 1 }), NullLogger<RabbitMqEventPublisher>.Instance);

        await sut.PublishAsync(DomainEvents.Create(EventTypes.LoginFailed, "User", Guid.Empty, null));
    }
}
