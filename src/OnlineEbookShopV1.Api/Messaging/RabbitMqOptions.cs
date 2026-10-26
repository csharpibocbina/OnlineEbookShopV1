namespace OnlineEbookShopV1.Api.Messaging;

public class RabbitMqOptions
{
    public const string Section = "RabbitMq";
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Exchange { get; set; } = "ebookshop.events";
    public string AuditQueue { get; set; } = "ebookshop.audit";
}
