using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using IdentityModel.Client;

namespace RestDemo
{
    public class SendAvl
    {
        
        public static bool SendEmployee(HttpClient client, Uri restUrl, string DeviceId, string DeviceType, string EmployeeId, double lat, double lon)
        {                    
            string commandPath = $"api/v3/UnitEmployee/{EmployeeId}/Devices/{DeviceId}/UpdateLocation";

             var dt = DateTimeOffset.Now.ToUnixTimeSeconds();

            var avlObject = new AvlObject(lat, lon, 5000, 10, 0, 166, dt, 5, 5, "", "TAIP", "DOP", "MLS", int.Parse(DeviceType));
    
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(restUrl + commandPath, UriKind.Absolute),
                Method = HttpMethod.Post
            };
             
            request.Content = new StringContent(JsonConvert.SerializeObject(avlObject, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));

            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var data = client.SendAsync(request).Result;
            Console.WriteLine("StatusCode: " + data.StatusCode);
            return (data.StatusCode == System.Net.HttpStatusCode.OK) ? true:false;
        }

        public static bool SendUnit(HttpClient client, Uri restUrl, string DeviceId, string DeviceType, string UnitId, double lat, double lon)
        {            
            string commandPath = $"api/v3/Unit/{UnitId}/Devices/{DeviceId}/UpdateLocation";

            var dt = DateTimeOffset.Now.ToUnixTimeSeconds();

            var avlObject = new AvlObject(lat, lon, 5000, 10, 0, 166, dt, 5, 5, "", "TAIP", "DOP", "MLS", int.Parse(DeviceType));
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(restUrl + commandPath, UriKind.Absolute),
                Method = HttpMethod.Post
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(avlObject, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));

            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var data = client.SendAsync(request).Result;
            Console.WriteLine("StatusCode: " + data.StatusCode);
            return (data.StatusCode == System.Net.HttpStatusCode.OK) ? true : false;

        }

        public static bool SendDevice(HttpClient client, Uri restUrl, string DeviceId, double lat, double lon)
        {            
            string commandPath = $"api/v3/Location/{DeviceId}/UpdateLocation";
         /*  string queryString = new QueryStringDict() {            
            { "Latitude", lat },
            { "Longitude", lon },            
            }.ToString();

            var data = client.PostAsync(restUrl + commandPath + "?" + queryString, null).Result;
            Console.WriteLine("StatusCode: " + data.StatusCode);
            return (data.StatusCode == System.Net.HttpStatusCode.OK) ? true : false;*/
            return false;
        }

    }
}
