//-----------------------------------------------------------------------
// <copyright file="DemoProgram.cs" company="Intergraph">
//     Copyright (c) Intergraph Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using IdentityModel.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

//
// Example C# to use the Hexagon Alerts REST interface
//
// Requires these nuget packages
//
//  "IdentityModel"   Version 3.10.10
//  "Newtonsoft.Json" Version 12.0.2
//  "RestSharp"       Version 106.4.2
//
// HELP FILE http://leooncalldev01.ingrnet.com:8080/api/documentation
//
namespace AlertsDemo
{
    public class DevelopmentConnection
    {
        private static HttpClient _client = new HttpClient();
        public int _sendCount { get; set; } = 1;
        private string _httpAndHostFulldomainName { get; set; }
        // Change this to your local machine name
        private const string AlertsApiPort = ":8080";
        private const string APIUser = "PROVIDERBRIDGE";
        private const string APISecret = "cad911";
        /*
        sample string HostUri = "http://leooncalldev01.ingrnet.com";
        */
        private string AccessToken = string.Empty;
        private int delayTime = 300;
        public DevelopmentConnection(string inputUrl, int sendCount)
        {
            
             _httpAndHostFulldomainName = inputUrl;            
            _sendCount = sendCount;
        }       

        public void Startup()
        {            
            if (_sendCount <= 0)
                _sendCount = 1;
            
            // Now get the Token from idenity service
            _client = new HttpClient();
            if (string.IsNullOrEmpty(_httpAndHostFulldomainName))
            {
                string globalConfigUrl = GetOnCallUrlFromGlobalConfig();
                //  WARNING The last char must match. not having the / can cause a discovery errors!            
                string slash = globalConfigUrl.Substring(globalConfigUrl.Length - 1, 1);
                if (0 != string.Compare(slash, "/"))
                    globalConfigUrl += "/";

                AccessToken = auth(globalConfigUrl);
                if(string.IsNullOrEmpty(AccessToken))
                    return;
            }
            else
            {
                string localUrl = _httpAndHostFulldomainName;
                //  WARNING The last char must match. not having the / can cause a discovery errors!            
                string slash = localUrl.Substring(localUrl.Length - 1, 1);
                if (0 != string.Compare(slash, "/"))
                    localUrl += "/";
                AccessToken = auth(localUrl + "oncall/");
                if (string.IsNullOrEmpty(AccessToken))
                    return;
            }

            //string tempUri = "http://leooncalldev01.ingrnet.com" + AlertsApiPort;
            //var APIServerUrl = new Uri(tempUri);

            // replace the https with http
            var noSSL = _httpAndHostFulldomainName.Replace("https", "http");
            //int pos = _httpAndHostFulldomainName.IndexOf("https");
            //if (pos > 0)
            //{
            //    _httpAndHostFulldomainName.Substring(0, pos) + "http" + _httpAndHostFulldomainName.Substring(pos + "https".Length);
            //}
            string tempUri = noSSL + AlertsApiPort; 
            var APIServerUrl = new Uri(tempUri);                       
           
            int retryCount = 1;
            HttpResponseMessage restapiResponse = null;
            while (retryCount < 10)
            {
                // Now we need to use the OIDC Token to get a OnCall Session Token
                restapiResponse = _client.PostAsync(APIServerUrl + "/api/StartSession" + $"?Token={AccessToken}", null).Result;

                if (restapiResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Connected in {retryCount} tries!");
                    break;
                }
                else
                {
                    Console.WriteLine($"Error talking to APIServer \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
                    retryCount++;
                    Thread.Sleep(1000);
                }
            }
            if(retryCount == 10 || restapiResponse == null)
            {
                Console.WriteLine($"Failed to connect in {retryCount} tries!");
                return;
            }

            // Now use the OnCall Session Token in the header (Authorization: bearer eyJhbG...) for all the API calls
            var restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
            var content = JsonConvert.DeserializeObject<JObject>(restcontent);
            var restToken = content["accessToken"].Value<string>();
            _client.SetBearerToken(restToken);

            // https://www.convertonline.io/convert/json-to-query-string
            // {"body": {"subject":"green","message":"blues","alertType":0,"priority": 0, "resource": null,"resourceType": 0,"applicationTerminal":null,"applicationName":"red","timerId": null, "custom_data": {}}
            // {"body": {"subject":"green","message":"blues","alertType":0,"alertVisibility": {"groups": [], "employees": [{"isFlagged": false,"employeeId": 2705}]} "priority": 0, "resource": null,"resourceType": 0, "applicationTerminal": null,    "applicationName": "red", "timerId": null, "custom_data": {}}

            // 0 = Unit Alert 
            // 1 = Event Alert
            // 2 = System Alert
            // 3 = Other Alerts
            // "description": "The type of alert that will be created. This determines which category it will appear on in the panel"

            // ------------------
            Console.WriteLine($"Sending 4 Alerts {_sendCount} times with {delayTime}ms delay between each Alert");
            int maxLoops = 4 * _sendCount;
            int sendingId = 0;
            string item = "Unit Alert";            
            for (int loops = 0; loops < maxLoops; loops++)
            {
                var UnitAlert = "{\"subject\":\"Unit   Alert "   + loops.ToString() + "\",\"message\":\"from RestApi Demo\",\"alertType\":0,\"alertVisibility\":{\"groups\":[],\"employees\":[{\"isFlagged\":false,\"employeeId\":2705}]},\"priority\":0,\"resource\":null,\"resourceType\":0,\"applicationTerminal\":\"dugs\",\"applicationName\":\"demo\",\"timerId\":null,\"custom_data\":{}}";
                var EventAlert = "{\"subject\":\"Event  Alert "  + loops.ToString() + "\",\"message\":\"from RestApi Demo\",\"alertType\":1,\"alertVisibility\":{\"groups\":[],\"employees\":[{\"isFlagged\":false,\"employeeId\":2705}]},\"priority\":0,\"resource\":null,\"resourceType\":0,\"applicationTerminal\":\"dugs\",\"applicationName\":\"demo\",\"timerId\":null,\"custom_data\":{}}";
                var SystemAlert = "{\"subject\":\"System Alert " + loops.ToString() + "\",\"message\":\"from RestApi Demo\",\"alertType\":2,\"alertVisibility\":{\"groups\":[],\"employees\":[{\"isFlagged\":false,\"employeeId\":2705}]},\"priority\":0,\"resource\":null,\"resourceType\":0,\"applicationTerminal\":\"dugs\",\"applicationName\":\"demo\",\"timerId\":null,\"custom_data\":{}}";
                var OtherAlert = "{\"subject\":\"Other  Alert "  + loops.ToString() + "\",\"message\":\"from RestApi Demo\",\"alertType\":3,\"alertVisibility\":{\"groups\":[],\"employees\":[{\"isFlagged\":false,\"employeeId\":2705}]},\"priority\":0,\"resource\":null,\"resourceType\":0,\"applicationTerminal\":\"dugs\",\"applicationName\":\"demo\",\"timerId\":null,\"custom_data\":{}}";
                JObject myContent = JObject.Parse(UnitAlert);
                switch (sendingId)
                {
                    case 0:
                        myContent = JObject.Parse(UnitAlert);
                        item = "Unit Alert";
                        break;
                    case 1:
                        myContent = JObject.Parse(EventAlert);
                        item = "Event Alert";
                        break;
                    case 2:
                        myContent = JObject.Parse(SystemAlert);
                        item = "System Alert";
                        break;
                    case 3:
                        myContent = JObject.Parse(OtherAlert);
                        item = "Other Alert";
                        break;
                    default:
                        sendingId = 0;
                        break;
                }
                sendingId++;

                var buffer = System.Text.Encoding.UTF8.GetBytes(myContent.ToString());
                var byteContent = new ByteArrayContent(buffer);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                restapiResponse = _client.PostAsync(APIServerUrl + "/api/v1/Alert", byteContent).Result;
                if (!restapiResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error Send Alert failed \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
                    break;
                }
                else
                {
                    Console.WriteLine($"/api/v1/Alert/? {item} = OK");
                    Thread.Sleep(delayTime);
                }
            }
            
            Console.WriteLine("done with commands! ");
            //Console.ReadLine();            
        }

        private string auth(string path)
        {
            string token = string.Empty;
            var idenityUrl = new Uri(path);
            var disco = _client.GetDiscoveryDocumentAsync(idenityUrl + "identity").Result;
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return token;
            }
            var tokenResponse = _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = APIUser, // Use the Client ID which is setup for the site. This is a ApiService account.
                ClientSecret = APISecret, // use the secret configured for your application (not hard coded)
                Scope = "api" // this scope is defined as the scope used for accessing the OnCall Web API
            }).Result;
            if (tokenResponse.IsError)
            {
                Console.WriteLine($"Error while contacting identity provider \n {tokenResponse.Error}");
                return token;
            }
            return tokenResponse.AccessToken;
        }


        /// <summary>
        /// Reads from the machine global config file settings.
        /// </summary>
        /// <returns>string url</returns>
        private static string GetOnCallUrlFromGlobalConfig()
        {            
            if (File.Exists(@"C:\ProgramData\Intergraph\Cad\cad-global.json.config"))
            {
                var configText = File.ReadAllText(@"C:\ProgramData\Intergraph\Cad\cad-global.json.config");
                var configData = (JObject)JsonConvert.DeserializeObject<JObject>(configText).First.First;
                var defaultConfig = configData["default"].Value<string>();
                return configData[defaultConfig]["ONCALL_BASE_URL"]?.Value<string>();
            }
            return "https://localhost:";
        }        
    }    
}
 