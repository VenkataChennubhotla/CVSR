namespace CNSDemo.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(CadEventHttpSubscriptionArgs))]
    public partial class CadEventHttpSubscriptionArgsContext : JsonSerializerContext { }

    [DataContract (Name = "CadEventHttpSubscriptionArgs")]
    public partial class CadEventHttpSubscriptionArgs
    {
        [JsonPropertyName("Response")]
        public required HttpResponseArgs Response { get; set; }

        [JsonPropertyName("Events")]
        public required IEnumerable<string> Events { get; set; }

        [JsonPropertyName("UserData")]
        public required string UserData { get; set; }
    }
}
