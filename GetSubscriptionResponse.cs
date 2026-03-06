namespace CNSDemo.Models
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(GetSubscriptionResponse))]
    public partial class GetSubscriptionResponseContext : JsonSerializerContext { }

    [DataContract(Name = "GetSubscriptionResponse")]
    public partial class GetSubscriptionResponse
    {
        [JsonPropertyName("subscriberId")]
        public string SubscriberId { get; set; }

        [JsonPropertyName("subscription")]
        public Object Subscription { get; set; }

        [JsonPropertyName("subscriptType")]
        public string SubscriptType { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("createdTime")]
        public DateTimeOffset CreatedTime { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("customData")]
        public string CustomData { get; set; }
    }
}
