namespace CNSDemo
{
    using System.Text.Json;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Microsoft.Extensions.Options;
    using IdentityModel.Client;
    using Microsoft.Extensions.Logging;
    using CNSDemo.Models;
    using CNSDemo.AppOptions;

    public class RestfulCadApi : IRestfulCadApi
    {
        private readonly ApplicationOptions _configuration;
        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory; //Avoid common DNS problems by managing HttpClient lifetimes - also don't use / dispose HttpClient because that can cause port exhaustion.

        public RestfulCadApi(IOptions<ApplicationOptions> configuration, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            _configuration = configuration.Value;
            _httpClientFactory = httpClientFactory;
           
            _logger = loggerFactory.CreateLogger<RestfulCadApi>();
        }

        public async Task<(bool, string, long)> StartSession()
        {
            try
            {
                _logger.LogInformation($"Starting Request to get DiscoveryDocument at {_configuration.OidcServerUri}");

                DiscoveryDocumentResponse disco = _httpClientFactory.CreateClient().GetDiscoveryDocumentAsync(_configuration.OidcServerUri).Result;

                if (disco.IsError)
                {
                    if (disco.Exception != null)
                    {
                        _logger.LogError(disco.Exception.ToString(), $"Error Retrieving Discovery doc from OIDC Server Uri {_configuration.OidcServerUri}");
                    }
                    else
                    {
                        _logger.LogError($"Unknown Error Retrieving Discovery doc from OIDC Server Uri {_configuration.OidcServerUri}");
                    }
                    return new(false, string.Empty, 0);
                }

                string clientId = _configuration.ClientId;
                string clientSecret = _configuration.ClientSecret;

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    _logger.LogError("Could not find the ClientId or ClientSecret  - Please verify these values are in the settings");
                    return new(false, string.Empty, 0);
                }

                _logger.LogInformation($"Logging in with {clientId}");

                var tokenResponse = await _httpClientFactory.CreateClient().RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    Scope = "api"
                });

                if (tokenResponse.IsError)
                {
                    _logger.LogError($"Error while contacting identity provider {_configuration.OidcServerUri}, using client id {clientId} response error: {tokenResponse.Error}");
                    return new(false, string.Empty, 0);
                }
                var oidcToken = tokenResponse.AccessToken;

                _logger.LogInformation($"Starting Session with RestAPI {_configuration.RestAPI}");
                var httpRequest = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                };

                httpRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue(_configuration.UserAgent, _configuration.ProductVersion));
                httpRequest.RequestUri = new Uri(_configuration.RestAPI, ("/api/startsession" + $"?Token={oidcToken}"));

                var restApiResponse = await _httpClientFactory.CreateClient().SendAsync(httpRequest);

                if (!restApiResponse.IsSuccessStatusCode)
                {
                    await LogResponseError(restApiResponse);
                    return new(false, string.Empty, 0);
                }
                var restcontent = await restApiResponse.Content.ReadAsStringAsync();
                var content = JsonSerializer.Deserialize(restcontent, typeof(AuthorizationResponse), AuthorizationResponseContext.Default);

                if (content is not AuthorizationResponse realContent)
                {
                    return new(false, string.Empty, 0);
                }

                var token = realContent.AccessToken ?? string.Empty;
                var expTime = realContent.ExpiresIn ?? -1;
                var timeNow = DateTimeOffset.Now.ToUnixTimeSeconds();

                _logger.LogInformation("Login Successful!");
                return (true, token, expTime + timeNow);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting restapi session: {ex.Message}");
                return new(false, string.Empty, 0);
            }
        }
        

        public async Task ClearSession(string token)
        {
            var httpRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Post
            };

            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
            httpRequest.RequestUri = new Uri(_configuration.RestAPI, "/api/ClearSession");
            var restApiResponse = await _httpClientFactory.CreateClient().SendAsync(httpRequest);
            if (!restApiResponse.IsSuccessStatusCode)
            {
                await LogResponseError(restApiResponse);
                return;
            }
        }


        private async Task LogResponseError(HttpResponseMessage restApiResponse)
        {
            _logger.LogError(@"Error talking to RestAPI {RestAPI}
                    {StatusCode} - {ReasonPhrase}
                    {Content}", _configuration.RestAPI, restApiResponse.StatusCode, restApiResponse.ReasonPhrase, await restApiResponse.Content.ReadAsStringAsync());
        }
    }
}

