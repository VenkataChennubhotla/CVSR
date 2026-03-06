namespace CNSDemo.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(HttpResponseArgs))]
    public partial class HttpResponseArgsContext : JsonSerializerContext { }

    [DataContract(Name = "HttpResponseArgs")]
    public class HttpResponseArgs
    {
        [JsonPropertyName("Type")]
        public string Type { get => "http"; }

        [JsonPropertyName("DisplayName")]
        public required string DisplayName { get; set; }

        [JsonPropertyName("Url")]
        public required string Url { get; set; }
    }
}
