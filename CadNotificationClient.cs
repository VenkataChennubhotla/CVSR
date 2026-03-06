namespace CNSDemo
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using CNSDemo.AppOptions;
    using CNSDemo.Models;

    public enum ResponseType
    {
        HTTP,
        RabbitMQ,
        AMQP,
        Cancel
    };

    public enum SubscriptionType
    {
        Subscribe,
        Resubscribe,
        Listen,
        HealthCheck,
        Unsubscribe,
        Cancel
    }

    public class CadNotificationClient
    {
        private readonly IRestfulCadApi _restfulCadApi;
        private readonly ICadNotificationApi _cnsApi;
        private readonly ApplicationOptions _configuration;
        private readonly ILogger _logger;

        private string _accessToken = string.Empty;
        private long _expTime = -1;
        internal int _circuitBreaker = 0;
        public bool IsAuthorized => _accessToken != string.Empty;
        public CadNotificationClient(IOptions<ApplicationOptions> configuration, IRestfulCadApi restfulcadApi, ICadNotificationApi cnsApi, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(restfulcadApi);
            ArgumentNullException.ThrowIfNull(cnsApi);
            ArgumentNullException.ThrowIfNull(configuration);
            _configuration = configuration.Value;
            _restfulCadApi = restfulcadApi;
            _cnsApi = cnsApi;

            _logger = loggerFactory.CreateLogger<CadNotificationClient>();
        }

        public async Task<bool> CheckAuthorizationAsync()
        {
            var timeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
            //If we have not authorized yet or the token is about to expire we gotta reauth.
            if (_accessToken == string.Empty || _expTime == -1 || _expTime <= timeNow - 60)
            {
                var successfulConnection = false;
                while (!successfulConnection)
                {
                    var results = await _restfulCadApi.StartSession();

                    if (results.Item1)
                    {
                        _circuitBreaker = 0;
                        _accessToken = results.Item2;
                        _expTime = results.Item3;
                        successfulConnection = true;
                        Console.WriteLine("Successfully started RestApi session");
                    }
                    else
                    {
                        Console.WriteLine("Failed to start RestApi session");
                        IncrementCircuitBreaker();
                        _logger.LogInformation($"Sending StartSession to restAPI failed currently at {_circuitBreaker}/{_configuration.RetryCount} failures." );
                    }
                }
            }

            return true;
        }

        public async Task<bool> Subscribe(ResponseType responseType)
        {
            await CheckAuthorizationAsync();
            return await _cnsApi.Subscribe(_accessToken, responseType);
        }

        public async Task<List<GetSubscriptionResponse>> GetSubscriptions()
        {
            await CheckAuthorizationAsync();
            var subscriptionList = await _cnsApi.GetSubscriptions(_accessToken);
            return subscriptionList;
        }

        public async Task<bool> Resubscribe(string subscriberId)
        {
            await CheckAuthorizationAsync();
            return await _cnsApi.Resubscribe(_accessToken, subscriberId);
        }

        public async Task<bool> Unsubscribe(string subscriberId)
        {
            await CheckAuthorizationAsync();
            return await _cnsApi.Unsubscribe(_accessToken, subscriberId);
        }

        public async Task RunClearSession()
        {
            await CheckAuthorizationAsync();
            await _restfulCadApi.ClearSession(_accessToken);
        }

        public async Task<string> RunHealthCheck()
        {
            await CheckAuthorizationAsync();
            return await _cnsApi.HealthCheck(_accessToken);
        }

        // This functionality is to prevent a service or other application to keep attempting to connect unsuccessfully to a
        // service that may be down. After attempting to connect the number of times in the retrycount, the application will stop.
        // this practice will be useful in a service, which will not allow for user input or constant observation
        private void IncrementCircuitBreaker()
        {
            _circuitBreaker += 1;

            if (_circuitBreaker > _configuration.RetryCount)
            {
                _logger.LogInformation("RetryCount of {RetryCount} was reached with out a successful communication with Restfulcad", _configuration.RetryCount);
                Environment.Exit(191519);
            }
        }
    }
}