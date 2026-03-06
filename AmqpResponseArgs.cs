namespace CNSDemo.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(AmqpResponseArgs))]
    public partial class AmqpResponseArgsContext : JsonSerializerContext { }

    [DataContract(Name = "AmqpResponseArgs")]
    public partial class AmqpResponseArgs
    {
        [JsonPropertyName("Type")]
        public string Type { get => "amqp"; }

        [JsonPropertyName("DisplayName")]
        public required string DisplayName { get; set; }

        [JsonPropertyName("Namespace")]
        public required string Namespace { get; set; }

        [JsonPropertyName("KeyName")]
        public required string KeyName { get; set; }

        [JsonPropertyName("KeyValue")]
        public required string KeyValue { get; set; }

        [JsonPropertyName("Address")]
        public required string Address { get; set; }

        [JsonPropertyName("IsPasswordEncrypted")]
        public required bool IsPasswordEncrypted { get; set; }
    }
}
