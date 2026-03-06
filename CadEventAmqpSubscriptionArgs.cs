namespace CNSDemo.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(CadEventAmqpSubscriptionArgs))]
    public partial class CadEventAmqpSubscriptionArgsContext : JsonSerializerContext { }

    [DataContract (Name = "CadEventAmqpSubscriptionArgs")]
    public partial class CadEventAmqpSubscriptionArgs
    {
        [JsonPropertyName("Response")]
        public required AmqpResponseArgs Response { get; set; }

        [JsonPropertyName("Events")]
        public required IEnumerable<string> Events { get; set; }

        [JsonPropertyName("UserData")]
        public required string UserData { get; set; }
    }
}
