using Newtonsoft.Json;
using RestSharp;
using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace RestDeccanDemo
{
    public class RestCallHandler
    {
        private const string _tokenPrefix = "Bearer ";
        private const string _tokenHeader = "Authorization";

        private RestClient _client { get; }
        private string _token { get; set; }
        private DateTime _tokenExpires { get; set; }
        private string _username { get; set; }
        private string _password { get; set; }

        public RestCallHandler(string baseAddress, string username, string password)
        {
            _client = new RestClient(baseAddress);

            if (baseAddress.Contains("https://"))
            {
                _client.RemoteCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback((sender, cert, certChain, policyError) =>
                {
                    //Here you can verify the certificate. Make sure issuers match.
                    return true;
                });
            }
            
            _username = username;
            _password = password;
        }

        private void Authorize()
        {
            var tokenRequest = new RestRequest("/api/Token", Method.POST);
            tokenRequest.AddHeader("content-type", "application/x-www-form-urlencoded");
            tokenRequest.AddParameter("application/x-www-form-urlencoded", $"grant_type=password&Username={_username}&Password={_password}", ParameterType.RequestBody);
            var response = _client.Execute(tokenRequest);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Failed to authorize for reason {response.StatusDescription}");
            }

            var tokenData = JsonConvert.DeserializeObject<Token>(response.Content);

            _token = tokenData.AccessToken;
            _tokenExpires = DateTime.Now.AddSeconds(tokenData.ExpiresIn);
        }

        private void CheckAuthorization()
        {
            if (_token == null || (DateTime.Now.Subtract(_tokenExpires).TotalMinutes < 3))
                Authorize();
        }

        public ActiveUnitData GetActiveUnit(string unitId)
        {
            CheckAuthorization();

            var request = new RestRequest($"/api/ActiveUnit/{unitId}", Method.GET);
            request.AddHeader(_tokenHeader, $"{_tokenPrefix}{_token}");
            var response = _client.Execute(request);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception($"Error calling GetActiveUnit reason {response.StatusDescription}");

            return JsonConvert.DeserializeObject<ActiveUnitData>(response.Content, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
        }

        public string GraphQLQuery(string query)
        {
            CheckAuthorization();

            var request = new RestRequest("/api/GraphQL", Method.POST);
            request.AddHeader(_tokenHeader, $"{_tokenPrefix}{_token}");
            request.AddParameter("text/plain", query, ParameterType.RequestBody);

            var response = _client.Execute(request);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception($"Error calling GraphQLQuery reason {response.StatusDescription}");

            return response.Content;
        }
    }
}