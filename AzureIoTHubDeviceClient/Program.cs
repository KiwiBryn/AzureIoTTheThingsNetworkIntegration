//---------------------------------------------------------------------------------
// Copyright (c) September 2020, devMobile Software
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
//---------------------------------------------------------------------------------
namespace devMobile.TheThingsNetwork.AzureIoTHubDeviceClient
{
   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Text;
   using System.Threading.Tasks;
   using Microsoft.Azure.Devices.Client;

   using Newtonsoft.Json;
   using Newtonsoft.Json.Linq;

   class Program
   {
      static async Task Main(string[] args)
      {
         string filename ;
         string azureIoTHubconnectionString;
         DeviceClient azureIoTHubClient;
         Payload payload;
         JObject telemetryDataPoint = new JObject();

         if (args.Length != 2)
         {
            Console.WriteLine("[JOSN file] [AzureIoTHubConnectionString] ");
            Console.WriteLine("  or");
            Console.WriteLine("[JOSN file] [AzureIoTCentralConnectionString] ");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
            return;
         }
         filename = args[0];
         azureIoTHubconnectionString = args[1];

         try
         {
            payload = JsonConvert.DeserializeObject<Payload>(File.ReadAllText(filename));

            JObject payloadFields = (JObject)payload.PayloadFields;

            using (azureIoTHubClient = DeviceClient.CreateFromConnectionString(azureIoTHubconnectionString, TransportType.Amqp_Tcp_Only))
            {
               await azureIoTHubClient.OpenAsync();

               foreach (JProperty child in payloadFields.Children())
               {
                  EnumerateChildren(telemetryDataPoint, child);
               }
               
               using (Message message = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryDataPoint))))
               {
                  Console.WriteLine(" {0:HH:mm:ss} AzureIoTHubDeviceClient SendEventAsync start", DateTime.UtcNow);
                  await azureIoTHubClient.SendEventAsync(message);
                  Console.WriteLine(" {0:HH:mm:ss} AzureIoTHubDeviceClient SendEventAsync finish", DateTime.UtcNow);
               }

               await azureIoTHubClient.CloseAsync();
            }
         }
			catch (Exception ex)
			{
            Console.WriteLine(ex.Message);
			}

         Console.WriteLine("Press <enter> to exit");
         Console.ReadLine();
         return;
      }

      static void EnumerateChildren(JObject jobject, JToken token)
      {
         if (token is JProperty property)
         {
            if (token.First is JValue)
            {
               jobject.Add(property.Name, property.Value);
            }
            else
            {
               JObject parentObject = new JObject();
               foreach (JToken token2 in token.Children())
               {
                  EnumerateChildren(parentObject, token2);
                  jobject.Add(property.Name, parentObject);
               }
            }
         }
         else
         {
            foreach (JToken token2 in token.Children())
            {
               EnumerateChildren(jobject, token2);
            }
         }
      }
   }

   public class Gateway
   {
      [JsonProperty("gtw_id")]
      public string GatewayId { get; set; }
      [JsonProperty("timestamp")]
      public long Timestamp { get; set; }
      [JsonProperty("time")]
      public DateTime Time { get; set; }
      [JsonProperty("channel")]
      public int Channel { get; set; }
      [JsonProperty("rssi")]
      public int Rssi { get; set; }
      [JsonProperty("snr")]
      public double Snr { get; set; }
      [JsonProperty("rf_Chain")]
      public int RFChain { get; set; }
      [JsonProperty("latitude")]
      public double Latitude { get; set; }
      [JsonProperty("longitude")]
      public double Longitude { get; set; }
      [JsonProperty("altitude")]
      public int Altitude { get; set; }
   }

   public class Metadata
   {
      [JsonProperty("time")]
      public string Time { get; set; }
      [JsonProperty("frequency")]
      public double Frequency { get; set; }
      [JsonProperty("modulation")]
      public string Modulation { get; set; }
      [JsonProperty("data_rate")]
      public string DataRate { get; set; }
      [JsonProperty("coding_rate")]
      public string CodingRate { get; set; }
      [JsonProperty("gateways")]
      public List<Gateway> Gateways { get; set; }
   }

   public class Payload
   {
      [JsonProperty("app_id")]
      public string ApplicationId{ get; set; }
      [JsonProperty("dev_id")]
      public string DeviceId { get; set; }
      [JsonProperty("hardware_serial")]
      public string HardwareSerial { get; set; }
      [JsonProperty("port")]
      public int Port { get; set; }
      [JsonProperty("counter")]
      public int Counter { get; set; }
      [JsonProperty("is_retry")]
      public bool IsRetry { get; set; }
      [JsonProperty("payload_raw")]
      public string PayloadRaw { get; set; }
      [JsonProperty("payload_fields")]
      public Object PayloadFields { get; set; }
      [JsonProperty("metadata")]
      public Metadata Metadata { get; set; }
      [JsonProperty("downlink_url")]
      public string Downlink_url { get; set; }
   }
}
