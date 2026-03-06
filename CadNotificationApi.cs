namespace CNSDemo
{
    using System.Text.Json;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Microsoft.Extensions.Options;
    using IdentityModel.Client;
    using Microsoft.Extensions.Logging;
    using System.Text.Json.Nodes;
    using CNSDemo.AppOptions;
    using CNSDemo.Models;

    public class CadNotificationApi : ICadNotificationApi
    {
        private readonly ApplicationOptions _configuration;
        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory; //Avoid common DNS problems by managing HttpClient lifetimes - also don't use / dispose HttpClient because that can cause port exhaustion.

        public CadNotificationApi(IOptions<ApplicationOptions> configuration, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            _configuration = configuration.Value;
            _httpClientFactory = httpClientFactory;
            _logger = loggerFactory.CreateLogger<RestfulCadApi>();
        }

        public async Task<List<GetSubscriptionResponse>> GetSubscriptions(string accessToken)
        {
            var subscriptionList = new List<GetSubscriptionResponse>();

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(_configuration.CnsUri, "/api/v1/Subscriptions"),
                Method = HttpMethod.Get
            };
            request.SetBearerToken(accessToken);
            var responseSubscriptions = _httpClientFactory.CreateClient().SendAsync(request).Result;

            if (!responseSubscriptions.IsSuccessStatusCode)
            {
                _logger.LogCritical("Error occured while api/v1/Subscriptions \n"
                    + $"Status Code: {responseSubscriptions.StatusCode} \n"
                    + $"Error: {responseSubscriptions.Content.ReadAsStringAsync().Result}");

                return subscriptionList;
            }

            try
            {
                var content = await responseSubscriptions.Content.ReadAsStringAsync();
                _logger.LogInformation($"api/v1/Subscriptions response: {content}");

                subscriptionList = (List<GetSubscriptionResponse>?)JsonSerializer.Deserialize(content, typeof(List<GetSubscriptionResponse>));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            if (subscriptionList != null)
            {
                return subscriptionList;
            }
            else
            {
                return [];
            }
        }

        public async Task<bool> Resubscribe(string accessToken, string subscriberId)
        {
            var subscribeRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(_configuration.CnsUri, $"/api/Subscribe/CadEvent/{subscriberId}"),
                Method = HttpMethod.Post
            };
            subscribeRequest.SetBearerToken(accessToken);
            var subscriptionsResponse = await _httpClientFactory.CreateClient().SendAsync(subscribeRequest);

            if (!subscriptionsResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"Error occured while {subscribeRequest.RequestUri} \n"
                    + $"Status Code: {subscriptionsResponse.StatusCode} \n"
                    + $"Error: {subscriptionsResponse.Content.ReadAsStringAsync().Result}");
                return false;
            }

            return true;
        }

        public async Task<bool> Subscribe(string accessToken, ResponseType responseType)
        {
            try
            {
                var notificationArray = JsonSerializer.Deserialize<JsonObject>(await File.ReadAllTextAsync("Notifications.json"));
                if (notificationArray == null)
                {
                    _logger.LogError("Unable to read list of notifications to subscribe to from notifications.json");
                    return false;
                }
                //there's probably a better way to do this
                var notificationList = JsonSerializer.Deserialize<List<string>>(notificationArray["notifications"]);
                if (notificationList == null)
                {
                    _logger.LogError("Unable to deserialize list of notifications to subscribe to from notifications.json");
                    return false;
                }

                var subscribeRequest = new HttpRequestMessage
                {
                    RequestUri = new Uri(_configuration.CnsUri, "/api/Subscribe/CadEvent"),
                    Method = HttpMethod.Post
                };

                switch (responseType)
                {
                    case (ResponseType.HTTP):
                        subscribeRequest = CreateHttpSubscribeRequest(notificationList, subscribeRequest);
                        break;
                    case (ResponseType.RabbitMQ):
                        subscribeRequest = CreateRabbitMQSubscribeRequest(notificationList, subscribeRequest);
                        break;
                    case (ResponseType.AMQP):
                        subscribeRequest = CreateAmqpSubscriberRequestMessage(notificationList, subscribeRequest);
                        break;
                    default:
                        return false;
                };

                subscribeRequest.SetBearerToken(accessToken);
                var subscriptionsResponse = _httpClientFactory.CreateClient().SendAsync(subscribeRequest).Result;

                if (!subscriptionsResponse.IsSuccessStatusCode)
                {
                    _logger.LogCritical("Error occurred while calling Cns endpoint api/v1/Subscriptions \n"
                        + $"Status Code: {subscriptionsResponse.StatusCode} \n"
                        + $"Error: {subscriptionsResponse.Content.ReadAsStringAsync().Result}");

                    Console.WriteLine($"A subscription may already exist for {_configuration.SubscriberDisplayName}. Either resubscribe or remove the subscription from the ExternalNotificationSubscriber table and restart cns");

                    return false;
                }
                var content = await subscriptionsResponse.Content.ReadAsStringAsync();

                _logger.LogInformation($"api/v1/Subscriptions response: {content}");
                Console.WriteLine($"api/v1/Subscriptions response: {content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem with subscription - please check log file");
                _logger.LogError($"Error reading Notifications.json: {ex.Message}");
            }

            return true;
        }

        public async Task<string> HealthCheck(string accessToken)
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(_configuration.CnsUri, "/api/HealthCheck"),
                Method = HttpMethod.Get
            };
            request.SetBearerToken(accessToken);
            var responseSubscriptions = await _httpClientFactory.CreateClient().SendAsync(request);

            if (!responseSubscriptions.IsSuccessStatusCode)
                _logger.LogError("Error occured while api/v1/Subscriptions \n"
                    + $"Status Code: {responseSubscriptions.StatusCode} \n"
                    + $"Error: {responseSubscriptions.Content.ReadAsStringAsync().Result}");

            return responseSubscriptions.Content.ReadAsStringAsync().Result;
        }

        public async Task<bool> Unsubscribe(string accessToken, string subscriberId)
        {
            var subscribeRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(_configuration.CnsUri, $"/api/Unsubscribe/{subscriberId}"),
                Method = HttpMethod.Post
            };
            subscribeRequest.SetBearerToken(accessToken);
            var subscriptionsResponse = await _httpClientFactory.CreateClient().SendAsync(subscribeRequest);

            if (!subscriptionsResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"Error occured while {subscribeRequest.RequestUri} \n"
                    + $"Status Code: {subscriptionsResponse.StatusCode} \n"
                    + $"Error: {subscriptionsResponse.Content.ReadAsStringAsync().Result}");
                return false;
            }
            return true;
        }

        private HttpRequestMessage CreateHttpSubscribeRequest(List<string> notificationList, HttpRequestMessage subscribeRequest)
        {
            var cadEventSubscriptionArgs = new CadEventHttpSubscriptionArgs
            {
                Events = notificationList,
                UserData = _configuration.SubscriberUserData,
                Response = new HttpResponseArgs()
                {
                    DisplayName = _configuration.SubscriberDisplayName,
                    Url = _configuration.HttpListenerOptions.HttpListenerUrl + "/ListenerPacket/"
                }
            };
            subscribeRequest.Content = new StringContent(JsonSerializer.Serialize(cadEventSubscriptionArgs, typeof(CadEventHttpSubscriptionArgs), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            subscribeRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return subscribeRequest;
        }

        private HttpRequestMessage CreateRabbitMQSubscribeRequest(List<string> notificationList, HttpRequestMessage subscribeRequest)
        {
            var cadEventSubscriptionArgs = new CadEventRabbitMQSubscriptionArgs()
            {
                Events = notificationList,
                UserData = _configuration.SubscriberUserData,
                Response = new RabbitMQResponseArgs()
                {
                    DisplayName = _configuration.SubscriberDisplayName,
                    ExchangeName = _configuration.RabbitMqOptions.RabbitMqExchange,
                    Hostnames = _configuration.RabbitMqOptions.RabbitMqHostName,
                    Password = _configuration.RabbitMqOptions.RabbitMqPassword,
                    QueueName = _configuration.RabbitMqOptions.RabbitMqQueue,
                    RoutingKey = _configuration.RabbitMqOptions.RabbitMqRoutingKey,
                    UserName = _configuration.RabbitMqOptions.RabbitMqUserName,
                    VirtualHost = _configuration.RabbitMqOptions.RabbitMqVirtualHost,
                    IsSslEnabled = _configuration.RabbitMqOptions.RabbitMqIsSslEnabled,
                    SslAcceptablePolicyErrors = _configuration.RabbitMqOptions.RabbitMqSslAcceptablePolicyErrors
                }
            };

            subscribeRequest.Content = new StringContent(JsonSerializer.Serialize(cadEventSubscriptionArgs, typeof(CadEventRabbitMQSubscriptionArgs), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            subscribeRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return subscribeRequest;
        }

        private HttpRequestMessage CreateAmqpSubscriberRequestMessage(List<string> notificationList, HttpRequestMessage subscribeRequest)
        {
            var cadEventSubscriptionArgs = new CadEventAmqpSubscriptionArgs()
            {
                Events = notificationList,
                UserData = _configuration.SubscriberUserData,
                Response = new AmqpResponseArgs()
                {
                   DisplayName = _configuration.SubscriberDisplayName,
                   Address = _configuration.AmqpOptions.AmqpAddress,
                   KeyName = _configuration.AmqpOptions.AmqpKeyName,
                   KeyValue = _configuration.AmqpOptions.AmqpKeyValue,  
                   Namespace = _configuration.AmqpOptions.AmqpNameSpace,
                   IsPasswordEncrypted = false
                }
            };

            subscribeRequest.Content = new StringContent(JsonSerializer.Serialize(cadEventSubscriptionArgs, typeof(CadEventAmqpSubscriptionArgs), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            subscribeRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return subscribeRequest;
        }
    }
}
