namespace CNSDemo.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(CadEventRabbitMQSubscriptionArgs))]
    public partial class CadEventRabbitMQSubscriptionArgsContext : JsonSerializerContext { }

    [DataContract (Name = "CadEventRabbitMQSubscriptionArgs")]
    public partial class CadEventRabbitMQSubscriptionArgs
    {
        [JsonPropertyName("Response")]
        public required RabbitMQResponseArgs Response { get; set; }

        [JsonPropertyName("Events")]
        public required IEnumerable<string> Events { get; set; }

        [JsonPropertyName("UserData")]
        public required string UserData { get; set; }
    }
}
