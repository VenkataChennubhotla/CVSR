namespace RestDemo
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System.Text.Json;

    public class RestfulCadClient
    {
        private readonly IRestfulCadApi _restfulCadApi;
        private readonly IOptions<ApplicationOptions> _configuration;
        private readonly ILogger _logger;

        private readonly JsonSerializerOptions _serializerOptions;
        private string _accessToken = string.Empty;
        private long _expTime = -1;
        internal int _circuitBreaker = 0;
        public bool IsAuthorized => _accessToken != string.Empty;
        public RestfulCadClient(IOptions<ApplicationOptions> configuration, IRestfulCadApi restfulcadApi, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(restfulcadApi);
            ArgumentNullException.ThrowIfNull(configuration);
            _configuration = configuration;
            _restfulCadApi = restfulcadApi;

            _logger = loggerFactory.CreateLogger<RestfulCadClient>();

            _serializerOptions = new JsonSerializerOptions()
            {
                WriteIndented = true
            };
        }
        public async Task<bool> CheckAuthorizationAsync()
        {
            var timeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
            //If we have not authorized yet or the token is about to expire we gotta reauth.
            if (_accessToken == string.Empty || _expTime == -1 || _expTime <= timeNow - 60)
            {
                var results = await _restfulCadApi.StartSession();

                if (results.Item1)
                {
                    _circuitBreaker = 0;
                    _accessToken = results.Item2;
                    _expTime = results.Item3;
                }
                else
                {
                    IncrementCircuitBreaker();
                    _logger.LogInformation(@"Sending StartSession to restAPI failed currently at {_circuitBreaker}/{MaxFails} failures.", _circuitBreaker, _configuration.Value.RetryCount);
                    return false;
                }
            }

            return true;
        }

        public async Task RunGetActiveUnits()
        {
            var activeUnitAgency = _configuration.Value.TestAgency;
            if (activeUnitAgency == null)
            {
                activeUnitAgency = "POLICE";
            }
            Console.WriteLine(""); //skip over the value the user typed
            Console.WriteLine("This endpoint will retreive all currently active units in the {0} Agency.", activeUnitAgency);
            await CheckAuthorizationAsync();
            var returnJsonString = await _restfulCadApi.GetActiveUnitsV2(_accessToken, activeUnitAgency);
            if (returnJsonString != null)
            {
                Console.Write(PrettyJson(returnJsonString));
            }
        }

        public async Task RunGetEvents()
        {
            var eventAgency = _configuration.Value.TestAgency;
            if (eventAgency == null)
            {
                eventAgency = "POLICE";
            }

            Console.WriteLine(""); //skip over the value the user typed
            Console.WriteLine("This endpoint will retreive all currently open events for the {0} Agency.", eventAgency);
            await CheckAuthorizationAsync();
            var returnJsonString = await _restfulCadApi.GetEventsV4(_accessToken, eventAgency);
            if (returnJsonString != null)
            {
                Console.Write(PrettyJson(returnJsonString));
            }
        }

        public async Task RunCreateEvent()
        {
            var eventType = _configuration.Value.TestEventType;
            if (eventType == null)
            {
                eventType = "BURGLARY";
            }

            var location = _configuration.Value.TestLocation;
            if (location == null)
            {
                location = "BURGLARY";
            }
            await CheckAuthorizationAsync();
            var returnJsonString = await _restfulCadApi.CreateEventv2(_accessToken, eventType, location);
            if (returnJsonString != null)
            {
                Console.Write(PrettyJson(returnJsonString));
            }
        }

        public async Task RunClearSession()
        {
            await CheckAuthorizationAsync();
            await _restfulCadApi.ClearSession(_accessToken);
        }

        // This functionality is to prevent a service or other application to keep attempting to connect unsuccessfully to a
        // service that may be down. After attempting to connect the number of times in the retrycount, the application will stop.
        // this practice will be useful in a service, which will not allow for user input or constant observation
        private void IncrementCircuitBreaker()
        {
            _circuitBreaker += 1;

            if (_circuitBreaker > _configuration.Value.RetryCount)
            {
                _logger.LogInformation("RetryCount of {RetryCount} was reached with out a successful communication with Restfulcad", _configuration.Value.RetryCount);
                Environment.Exit(191519);
            }
        }

        public string PrettyJson(string unPrettyJson)
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(unPrettyJson);

            return JsonSerializer.Serialize(jsonElement, _serializerOptions);
        }
    }
}
