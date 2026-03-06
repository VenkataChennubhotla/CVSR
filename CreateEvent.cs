namespace RestDemo.Models
{
    //To help with converting a JSON object into a c# class visit https://json2csharp.com/.
    //paste the json to convert and it will generate a class. Check Use JsonPropertyName (.NET Core). checkbox
    //for information about JsonSerializerContext class visit https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation?pivots=dotnet-8-0
    //These models will help with serializing (class object to json string) and deserializing (reading json into a class object to use in your application)
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(CreateEventRequest))]
    public partial class CreateEventRequestContext : JsonSerializerContext { }

    [DataContract(Name = "LocationArgs")]
    public partial class LocationArgs
    {
        [JsonPropertyName("Latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("Longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("Location")]
        public string Location { get; set; }
    }

    [DataContract(Name = "CreateEventRequest")]
    public partial class CreateEventRequest
    { 
        [JsonPropertyName("Latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("Longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("Location")]
        public LocationArgs Location { get; set; }

        [JsonPropertyName("EventType")]
        public string EventType { get; set; }

        [JsonPropertyName("EventSubType")]
        public string EventSubType { get; set; }
    }

}
