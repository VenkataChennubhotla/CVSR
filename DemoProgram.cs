//-----------------------------------------------------------------------
// <copyright file="DemoProgram.cs" company="Intergraph">
//     Copyright (c) Intergraph Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
//using Amqp;
using IdentityModel.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// Example C# to use the Hexagon Deccan REST interface
//
// Requires these nuget packages
//
//  "IdentityModel"   Version 4.4.0
//  "Newtonsoft.Json" Version 12.0.2
//  "RestSharp"       Version 106.4.2
//
//
namespace DeccanDemo
{
    public class DevelopmentConnection
    {
        // This is written to allow testing with the DeccanApi server running local or remote.
        // You have to hardcode which to use.
        private static HttpClient _client;

        // Change this to your local machine name
        private const string LocalHost = "https://localhost";
       
        private const string DeccanApiPort = ":8080";
        private const string CNSPort = ":8081";
        private const string MyCallbackPort = ":8085"; 
        private const string APIUser = "apitest";
        private const string APISecret = "cad911";

        private HttpListener _listener;
        private bool _stopPacketListenThread = false;
        private  CancellationTokenSource _listenerCNSCancellationTokenSource;
        private readonly Thread _listenerCNSSubscriptionThread; //Thread for listening for packets from Cad Notification Service

        static void ExitLine(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("Press Enter to exit");
            Console.ReadLine();
            Environment.Exit(0);
        }

        public DevelopmentConnection()
        {
            //setup for thread that listens for CNS packets
            ThreadStart listenerCNSThreadStart = new ThreadStart(CNSListenerThread);
            _listenerCNSSubscriptionThread = new Thread(listenerCNSThreadStart);
            _listenerCNSCancellationTokenSource = new CancellationTokenSource();
        }

        public void Startup()
        {
            string urlFromGlobal = GetOnCallUrlFromGlobalConfig();
            Uri onCallUrl = new Uri(urlFromGlobal);
            Uri restApiUrl = new Uri("https://" +  onCallUrl.Host + DeccanApiPort);
            Uri cnsUrl = new Uri("https://" + onCallUrl.Host + CNSPort);
            _client = new HttpClient();

            // Steps to retreive authorization token from Identity Server
            DiscoveryDocumentResponse disco = _client.GetDiscoveryDocumentAsync("https://" + onCallUrl.Authority + "/oncall.identity").Result;

            if (disco.IsError)
                ExitLine($"Error Retrieving Discovery doc \n" + disco.Error);

            var tokenResponse = _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = APIUser,
                ClientSecret = APISecret,
                Scope = "api"
            }).Result;

            if (tokenResponse.IsError)
                ExitLine($"Error while contacting identity provider \n {tokenResponse.Error}");

            var oidcToken = tokenResponse.AccessToken;

            //Sending restapi the token - start session

            var restapiResponse = _client.PostAsync(restApiUrl + "api/startsession" + $"?Token={oidcToken}", null).Result;

            if (!restapiResponse.IsSuccessStatusCode)
                ExitLine($"Error talking to RestAPI \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");

            var restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
            var content = JsonConvert.DeserializeObject<JObject>(restcontent);
            var restAPIToken = content["accessToken"].Value<string>();

            //setting up Cad Notification Service subscription
            var request = CadEventSubscription(cnsUrl);

            // Open a HTTPListner to listen for data from CNS.
            // This should be Opened BEFORE subscribing to CNS.
            _listener = new HttpListener();
            _listener.Prefixes.Add("https://+:8085/ListenerPacket/");
            _listener.Start();

            //// Execute the subscription request.
            //Execute the subscription request.
            request.SetBearerToken(restAPIToken);
            var response = _client.SendAsync(request).Result;

            if (!response.IsSuccessStatusCode)
                ExitLine("Error occured while subscribing \n"
                    + $"Status Code: {response.StatusCode} \n"
                    + $"Error: {response.Content.ReadAsStringAsync().Result}");

            Console.WriteLine($"Subscription response: {response.Content.ReadAsStringAsync().Result}");

            //Successfully subscribed to CNS so start the thread that listens for CNS data
            _listenerCNSSubscriptionThread.Start();

            // We need to keep these cached in order to translate event and unit status codes to strings
            // I'm not caching but you should.

            List<JObject> allEventCodes = new List<JObject>();
            List<JObject> allUnitCodes = new List<JObject>();

            // Now use the OnCall Session Token in the header (Authorization: bearer eyJhbG...) for all the API calls

            _client.SetBearerToken(restAPIToken);

            /*
             * If you want to use command line curl to test, replace the token with the one returned in restToken
             * curl -X GET "http://localhost:8088/api/v1/Event" -H "accept: application/json" -H "Authorization: bearer eyJhbGciOiJodHRwOi8vd3d3LnczLm9yZy8yMDAxLzA0L3htbGRzaWctbW9yZSNobWFjLXNoYTI1NiIsInR5cCI6IkpXVCJ9.eyJzZXNzaW9uSWQiOiJiZDc3ZmUzMy1mMTFhLTQ1NTAtOWQ1Ny1mYzhmNDg3ZTg5YTUiLCJFbXBsb3llZUlkIjoiMCIsIm5iZiI6MTU2MDgwNDIzNiwiZXhwIjoxNTYwODUxMDM2LCJpc3MiOiJSZXN0ZnVsQ2FkIiwiYXVkIjoiYWxsIn0.KAhG3Vy-3BCRK1JJPiR7n-SBLCaR2FLzLDP41ofr5Pg"
             */

            // Get Event Status Codes
            //  "numericStatusCode": 17,
            //  "statusCode": "AwaitingClosure",
            //  "definition": "Awaiting Closure",
            //  "mnemonic": "W",
            //  "color": "rgb(0,0,0)"
            restapiResponse = _client.GetAsync(restApiUrl + "api/v1/Event/StatusCodes").Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                ExitLine($"Error get events status codes\n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                Console.WriteLine($"api/v1/Event/StatusCodes = OK");
                restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
                allEventCodes = JsonConvert.DeserializeObject<List<JObject>>(restcontent);
                Console.WriteLine($"/api/v1/Event/StatusCodes results");
                allEventCodes.ForEach(Console.WriteLine);
            }

            // all events in all agencies
            restapiResponse = _client.GetAsync(restApiUrl + "api/v1/Event").Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                ExitLine($"Error getting all events \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
                var allEvents = JsonConvert.DeserializeObject<List<JObject>>(restcontent);
                Console.WriteLine($"/api/v1/Event results count = {allEvents.Count}");
                if (allEvents.Count > 0)
                    allEvents.ForEach(Console.WriteLine);
            }

            // all events in POLICE agency - Example data 
            // "agencyEventId": "P18021949917",
            // "agencyId": "POLICE",
            // "dispatchGroup": "1",
            // "typeCode": "10-31",
            // "typeCodeDescription": "DOMESTIC TROUBLE",
            // "subTypeCode": "TROUBLE",
            // "subTypeCodeDescription": "DOMESTIC TROUBLE",
            // "latitude": 38.256032,
            // "longitude": -85.751161,
            // "locationText": "LL(38.256032,-85.751161)",
            // "statusCode": 17,
            // "status": "AwaitingClosure",
            // "description": null,
            // "priority": 3,
            // "isOpen": true
            restapiResponse = _client.GetAsync(restApiUrl + "api/v1/Event?AgencyId=POLICE").Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error getting all POLICE events \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
                var allEvents = JsonConvert.DeserializeObject<List<JObject>>(restcontent);
                Console.WriteLine($"/api/v1/Event?AgencyId=POICE results {allEvents.Count}");
                if (allEventCodes.Count > 0 && allEvents.Count > 0)
                {
                    allEvents.ForEach(Console.WriteLine);
                    // Get to one value in the array
                    var EventId = allEvents[0]["agencyEventId"].Value<string>();
                    var EventStatusCode = allEvents[0]["statusCode"].Value<int>();
                    var row = allEventCodes.FirstOrDefault(r => r["numericStatusCode"].Value<int>() == EventStatusCode);
                    if (row != null)
                        Console.WriteLine($"Event {EventId} is in status {row["statusCode"]}");
                    else
                        Console.WriteLine($"Event {EventId} is in unknown status");
                }
            }

            // all units - Example data
            // "agencyId": "LFD",
            // "dispatchGroup": "LFD",
            // "latitude": 38.255168278287442,
            // "longitude": -85.769180456154544,
            // "location": "1135 W JEFFERSON ST",
            // "status": 0,
            // "statusChangeTime": "2019-10-31T10:39:13-05:00",
            // "assignedAgencyEventId": null,
            // "assignedAgencyEventSubtypeCode": null,
            // "assignedSubtypeCodeDescription": null,
            // "assignedAgencyEventTypeCode": null,
            // "assignedTypeCodeDescription": null,
            // "statusedAgencyEventId": null,
            // "statusedAgencyEventSubtypeCode": null,
            // "statusedAgencyEventTypeCode": null,
            // "unitId": "DECCAN1",
            // "vehicleId": null,
            // "stationId": "LFD2",
            // "permanentStationId": "LFD2",
            // "stagingAreaId": null,
            // "bay": "1",
            // "unitType": "ENGINE"
            restapiResponse = _client.GetAsync(restApiUrl + "api/v1/Unit").Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error getting all units \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
                var allUnits = JsonConvert.DeserializeObject<List<JObject>>(restcontent);
                Console.WriteLine($"/api/v1/Unit results {allUnits.Count}");
                if (allUnits.Count > 0)
                    allUnits.ForEach(Console.WriteLine);
            }

            // all units in agency FIRE
            restapiResponse = _client.GetAsync(restApiUrl + "api/v1/Unit?AgencyId=FIRE").Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error getting all FIRE units \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
                var allUnits = JsonConvert.DeserializeObject<List<JObject>>(restcontent);
                Console.WriteLine($"/api/v1/Unit?AgencyId=FIRE results {allUnits.Count}");
                if (allUnits.Count > 0)
                    allUnits.ForEach(Console.WriteLine);
            }

            // Get Unit Status Codes            
            restapiResponse = _client.GetAsync(restApiUrl + "api/v1/Unit/StatusCodes").Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error get unit status codes\n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                Console.WriteLine($"/api/v1/Unit/StatusCodes = OK");
                restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
                allUnitCodes = JsonConvert.DeserializeObject<List<JObject>>(restcontent);
                Console.WriteLine($"/api/v1/Unit/StatusCodes results");
                allUnitCodes.ForEach(Console.WriteLine);
            }

            // Relocate a unit - bad request indicates the stations may be incorrect
            string unitId = "F100"; // unit is in station FST1 so lets relocate him to FST2
            string stationId = "FST2";
            restapiResponse = _client.PostAsync(restApiUrl + "api/v1/Unit/" + unitId + "/Relocate?StationId=" + stationId, null).Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error Relocate Unit\n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                Console.WriteLine($"/api/v1/Unit/{unitId}/Relocate/?StationId={stationId} = OK");
            }

            // Relocate Back a unit            
            restapiResponse = _client.PostAsync(restApiUrl + "api/v1/Unit/" + unitId + "/Relocate?ReturnHome=true", null).Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error Relocate Unit Back failed \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                Console.WriteLine($"/api/v1/Unit/{unitId}/Relocate/?ReturnHome=true = OK");
            }

            // Send Message - the body of the request contains the parameters
            // Dest Types
            // ------------------           
            // AlternateEmpIDs
            // EmpIDs
            // Emails
            // AlphaPagers
            // PersonalPagers
            // StationPrintQueues
            // StationPagers
            // Groups
            // Terminals
            // ------------------
    
            var sendMessageArguments = new SendMessageArgs()
            {
                Subject = "Message To GROUP",
                Priority= 0,
            };

            sendMessageArguments.MessageBody = new List<string>()
            {
                "HELLO DISPATCHER OF POLICE GROUP NE&Priority=4"
            };

            //dynamic and static groups
            sendMessageArguments.Groups = new List<string>()
            {
                "POLICE:NE:disp"
            };

            request = new HttpRequestMessage
            {
                RequestUri = new Uri(restApiUrl + "api/v1/Messages/SendMessage", UriKind.Absolute),
                Method = HttpMethod.Post
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(sendMessageArguments, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            restapiResponse = _client.SendAsync(request).Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error Send Message failed \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            
            {
                Console.WriteLine($"/api/v1/Messages/SendMessage to Groups - OK");
            }

            // Send Message  to employee          
            sendMessageArguments = new SendMessageArgs()
            {
                Subject = "Message To EMPLOYEE",
                Priority = 0,
            };

            sendMessageArguments.MessageBody = new List<string>()
            {
                "HELLO DISPATCHER "
            };

            //dynamic and static groups
            sendMessageArguments.EmpIDs = new List<string>()
            {
                "1"
            };

            request = new HttpRequestMessage
            {
                RequestUri = new Uri(restApiUrl + "api/v1/Messages/SendMessage", UriKind.Absolute),
                Method = HttpMethod.Post
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(sendMessageArguments, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            restapiResponse = _client.SendAsync(request).Result;
            if (!restapiResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error Send Message failed \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else

            {
                Console.WriteLine($"/api/v1/Messages/SendMessage to employee - OK");
            }

            // Get Unit Equipment. Note vehicles have equipment so a unit must have a vehicle associated to have equipment
             //           {
            //                "equipmentCount": 6,
            //                 "equipmentName": "COATS",
            //                 "unitId": "F100"
            //            }
            //            {
            //                "equipmentCount": 10,
            //                 "equipmentName": "Extinguishers",
            //                  "unitId": "F100"
            //            }
            //            {
            //                "equipmentCount": 6,
            //                 "equipmentName": "HOSE",
            //                "unitId": "F100"
            //            }

            restapiResponse = _client.GetAsync(restApiUrl + "api/v1/Unit/" + unitId + "/Equipment").Result;

            if (!restapiResponse.IsSuccessStatusCode)
            {
                ExitLine($"Error get unit equipment \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                Console.WriteLine($"api/v1/Unit/" + unitId + "/Equipment = OK");
                restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
                allEventCodes = JsonConvert.DeserializeObject<List<JObject>>(restcontent);
                Console.WriteLine($"api/v1/Unit/" + unitId + "Equipment results");
                allEventCodes.ForEach(Console.WriteLine);
            }

         //Get Employee Skills
            //{
            //  "employeeId": "16390",
            //  "description": "This is a comment",
            //  "skillName": "BOBSLED",
            //  "skillPriority": "2",
            //  "customData": null
            //}

            var employeeId = 16390;
            restapiResponse = _client.GetAsync(restApiUrl + "api/v1/Employee/" + employeeId + "/Skill").Result;

            if (!restapiResponse.IsSuccessStatusCode)
            {
                ExitLine($"Error get employee Skill \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
            }
            else
            {
                Console.WriteLine($"api/v1/Employee/" + employeeId + "/Skill = OK");
                restcontent = restapiResponse.Content.ReadAsStringAsync().Result;
                allEventCodes = JsonConvert.DeserializeObject<List<JObject>>(restcontent);
                Console.WriteLine($"api/v1/Employee/" + employeeId + "Skill results");
                allEventCodes.ForEach(Console.WriteLine);
            }

            Console.WriteLine("done with commands! Hit Enter to Exit");
            Console.ReadLine();
            _stopPacketListenThread = true;
            _listenerCNSCancellationTokenSource.Cancel();
        }

        
        private async void CNSListenerThread()
        {
            _stopPacketListenThread = false;
            try
            { 
                // Loop forever listening for data on the httplistener.
                while (!_stopPacketListenThread)
                {
                    var context = await GetListenerContextAsync(_listener);
                    using (var sr = new StreamReader(context.Request.InputStream))
                    {
                        var data = await sr.ReadToEndAsync();
                        // Highly suggested that at this point data should be added to a queue.
                        // With a consumer reading off the queue to keep from holding several tcp connections open.
                        Console.WriteLine(data);
                        Console.WriteLine("\n");
                    }
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.Close();
                }
            }
            catch (TaskCanceledException)
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task<HttpListenerContext> GetListenerContextAsync(HttpListener httpListener)
        {
            var taskCompletionSource = new TaskCompletionSource<HttpListenerContext>(); //wrapping a Task to manually control
            _listenerCNSCancellationTokenSource.Token.Register(() => taskCompletionSource.TrySetCanceled());
            var task = httpListener.GetContextAsync();
            var completedTask = await Task.WhenAny(task, taskCompletionSource.Task);
            if (completedTask == task)
            {
                var result = await task;
                taskCompletionSource.TrySetResult(result);
            }

            return await taskCompletionSource.Task;
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
            return LocalHost; 
        }

        private static HttpRequestMessage CadEventSubscription(Uri cnsUrl )
        {
            var responseArgs = new HttpResponseArgs()
            {
                DisplayName = "Deccan CNS Demo",
                Url = $"https://{Environment.MachineName}:8085/ListenerPacket/"
            };

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(cnsUrl + "api/Subscribe/CadEvent", UriKind.Absolute),
                Method = HttpMethod.Post
            };

            var test = new CadEventSubscriptionArgs
            {
                Response = responseArgs,
                Events = new List<string> { "Dispatched", "EventCreated", "EmergencyEvent", "EventUpdate", "UnitStatus", "FieldEvent", "Relocate" }
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(test, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            return request;
        }
    }
}
 