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
namespace devMobile.TheThingsNetwork.AzureIoTHubDeviceGatewayClient
{
   using System;
   using System.IO;
   using System.Text;
   using System.Threading.Tasks;
   using Microsoft.Azure.Devices.Client;

   using Newtonsoft.Json;

   class Program
   {
      static async Task Main(string[] args)
      {
         string filename;
         string azureIoTHubconnectionString;
         string deviceID;
         DeviceClient azureIoTHubClient;

         if (args.Length != 3)
         {
            Console.WriteLine("[JOSN file] [AzureIoTHubConnectionString] [deviceID]");
            Console.WriteLine("  or");
            Console.WriteLine("[JOSN file] [AzureIoTCentralConnectionString] [deviceID]");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
            return;
         }
         filename = args[0];
         azureIoTHubconnectionString = args[1];
         deviceID = args[2];

         try
         {
            string payload = File.ReadAllText(filename);

            using (azureIoTHubClient = DeviceClient.CreateFromConnectionString(azureIoTHubconnectionString, deviceID))
            {
               azureIoTHubClient.OperationTimeoutInMilliseconds = 5000;

               await azureIoTHubClient.OpenAsync();

               using (Message message = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(payload))))
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
      }
   }
}
