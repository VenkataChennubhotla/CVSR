namespace CNSDemo.Models
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(AuthorizationResponse))]
    public partial class AuthorizationResponseContext : JsonSerializerContext { }

    /// <summary>
    /// AuthorizationResponse
    /// </summary>
    public partial class AuthorizationResponse
    {
        /// <summary>
        /// Gets or Sets AccessToken
        /// </summary>
        [JsonPropertyName("accessToken")]
        public required string AccessToken { get; set; }

        /// <summary>
        /// Gets or Sets TokenType
        /// </summary>
        [JsonPropertyName("tokenType")]
        public required string TokenType { get; set; }

        /// <summary>
        /// Gets or Sets ExpiresIn
        /// </summary>
        [JsonPropertyName("expiresIn")]
        public int? ExpiresIn { get; set; }
    }
}
