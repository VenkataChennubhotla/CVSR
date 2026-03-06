namespace CNSDemo.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(RabbitMQResponseArgs))]
    public partial class RabbitMQResponseArgsContext : JsonSerializerContext { }

    [DataContract(Name = "RabbitMQResponseArgs")]
    public class RabbitMQResponseArgs 
    {
        [JsonPropertyName("Type")]
        public string Type { get => "rabbitmq"; }

        [JsonPropertyName("DisplayName")]
        public required string DisplayName { get; set; }

        [JsonPropertyName("Hostnames")]
        public required string Hostnames { get; set; }

        [JsonPropertyName("UserName")]
        public required string UserName { get; set; }

        [JsonPropertyName("Password")]
        public required string Password { get; set; }

        [JsonPropertyName("VirtualHost")]
        public required string VirtualHost { get; set; }

        [JsonPropertyName("ExchangeName")]
        public required string ExchangeName { get; set; }

        [JsonPropertyName("QueueName")]
        public required string QueueName { get; set; }

        [JsonPropertyName("RoutingKey")]
        public required string RoutingKey { get; set; }

        [JsonPropertyName("IsSslEnabled")]
        public bool IsSslEnabled { get; set; }

        [JsonPropertyName("SslAcceptablePolicyErrors")]
        public string? SslAcceptablePolicyErrors { get; set; }
    }
}
