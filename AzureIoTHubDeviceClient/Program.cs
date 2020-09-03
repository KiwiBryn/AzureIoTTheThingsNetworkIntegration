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

            JObject payloadFields = (JObject)payload.payload_fields;

            using (azureIoTHubClient = DeviceClient.CreateFromConnectionString(azureIoTHubconnectionString, TransportType.Amqp))
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
      public string gtw_id { get; set; }
      public long timestamp { get; set; }
      public DateTime time { get; set; }
      public int channel { get; set; }
      public int rssi { get; set; }
      public double snr { get; set; }
      public int rf_chain { get; set; }
      public double latitude { get; set; }
      public double longitude { get; set; }
      public int altitude { get; set; }
   }

   public class Metadata
   {
      public string time { get; set; }
      public double frequency { get; set; }
      public string modulation { get; set; }
      public string data_rate { get; set; }
      public string coding_rate { get; set; }
      public List<Gateway> gateways { get; set; }
   }

   public class Payload
   {
      public string app_id { get; set; }
      public string dev_id { get; set; }
      public string hardware_serial { get; set; }
      public int port { get; set; }
      public int counter { get; set; }
      public bool is_retry { get; set; }
      public string payload_raw { get; set; }
      public Object payload_fields { get; set; }
      public Metadata metadata { get; set; }
      public string downlink_url { get; set; }
   }
}
