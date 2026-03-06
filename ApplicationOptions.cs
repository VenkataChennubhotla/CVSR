namespace CNSDemo.AppOptions
{
    using Microsoft.Extensions.Options;
    using System.ComponentModel.DataAnnotations;

    public sealed class ApplicationOptions
    {
        public const string ConfigurationSectionName = "Application";

        [Required]
        [RegularExpression(@"((\w+:\/\/)[-a-zA-Z0-9:@;?&=\/%\+\.\*!'\(\),\$_\{\}\^~\[\]`#|]+)")]
        public required string RestApiServerUri { get; set; }

        public Uri RestAPI => new(RestApiServerUri);

        [Required]
        [RegularExpression(@"((\w+:\/\/)[-a-zA-Z0-9:@;?&=\/%\+\.\*!'\(\),\$_\{\}\^~\[\]`#|]+)")]
        public required string CnsServerUri { get; set; }

        public Uri CnsUri => new(CnsServerUri);

        [Required]
        [RegularExpression(@"((\w+:\/\/)[-a-zA-Z0-9:@;?&=\/%\+\.\*!'\(\),\$_\{\}\^~\[\]`#|]+)")]
        public required string OidcServerUri { get; set; }

        public Uri OidcServer => new(OidcServerUri);

        [Required]
        public required string ClientId { get; set; }

        [Required]
        public required string ClientSecret { get; set; }

        [Required]
        public required int RetryCount { get; set; }

        [Required]
        public required string SubscriberDisplayName { get; set; }

        [Required]
        public required string SubscriberUserData { get; set; }

        [Required]
        public required string UserAgent { get; set; }

        [Required]
        public required string ProductVersion { get; set; }

        public RabbitMqOptions RabbitMqOptions { get; set; }

        public AmqpOptions AmqpOptions { get; set; }

        public HttpListenerOptions HttpListenerOptions { get; set; }    

    }

    //This is a marker for the Validation sourcecode generator.
    [OptionsValidator]
    public partial class ValidateSettingsOptions : IValidateOptions<ApplicationOptions>
    {
    }
}