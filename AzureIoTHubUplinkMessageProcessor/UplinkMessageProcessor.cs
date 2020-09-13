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
namespace devMobile.TheThingsNetwork.AzureStorageQueueProcessorFunction
{
   using System;
   using System.Collections.Concurrent;
   using System.Security.Cryptography;
   using System.Text;
   using System.Threading.Tasks;
   using Microsoft.Azure.Devices.Client;
   using Microsoft.Azure.Devices.Provisioning.Client;
   using Microsoft.Azure.Devices.Provisioning.Client.Transport;
   using Microsoft.Azure.Devices.Shared;
   using Microsoft.Azure.Storage.Queue;
   using Microsoft.Azure.WebJobs;
   using Microsoft.Extensions.Configuration;
   using Microsoft.Extensions.Logging;
   using Newtonsoft.Json;
   using Newtonsoft.Json.Linq;

   using devMobile.TheThingsNetwork.AzureStorageQueueProcessorFunction.Models;

   public static class UplinkMessageProcessor
   {
      static readonly ConcurrentDictionary<string, DeviceClient> DeviceClients = new ConcurrentDictionary<string, DeviceClient>();

      [FunctionName("UplinkMessageProcessor")]
      [Singleton] // used when debugging
      public static async Task Run(
         [QueueTrigger("%UplinkQueueName%", Connection = "AzureStorageConnectionString")]
         CloudQueueMessage cloudQueueMessage, // Used to get CloudQueueMessage.Id for logging
         Microsoft.Azure.WebJobs.ExecutionContext context,
         ILogger log)
      {
         PayloadV5 payloadObect;
         DeviceClient deviceClient = null;
         string globalDeviceEndpoint;
         string enrollmentGroupSymmetricKey;
         string scopeID;
         int deviceProvisioningPollingDelay;

      // Load configuration for DPS 
      var configuration = new ConfigurationBuilder()
            //.SetBasePath(context.FunctionAppDirectory) bring these back for advanced configuration
            //.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

         try
         {
            globalDeviceEndpoint = configuration.GetSection("GlobalDeviceEndpoint").Value;
            enrollmentGroupSymmetricKey = configuration.GetSection("enrollmentGroupSymmetricKey").Value;
            scopeID = configuration.GetSection("ScopeID").Value;
            deviceProvisioningPollingDelay = int.Parse(configuration.GetSection("DeviceProvisioningPollingDelay").Value);
         }
         catch (Exception ex)
         {
            log.LogError(ex, $"Error loading configuration globalDeviceEndpoint and/or enrollmentGroupKey and/or scopeID and/or deviceProvisiongPollingDelay not configured");
            throw;
         }

         try
         {
            payloadObect = JsonConvert.DeserializeObject<PayloadV5>(cloudQueueMessage.AsString);
         }
         catch (Exception ex)
         {
            log.LogError(ex, $"MessageID:{cloudQueueMessage.Id} payload deserialisation failed");
            throw;
         }

         // Extract the device ID as it's used lots of places
         string registrationID = payloadObect.hardware_serial;

         // Construct the prefix used in all the logging
         string messagePrefix = $"MessageID: {cloudQueueMessage.Id} DeviceID:{registrationID} Counter:{payloadObect.counter} Application ID:{payloadObect.app_id}";
         log.LogInformation(messagePrefix);

         if (DeviceClients.TryAdd(registrationID, deviceClient))
         {
            log.LogInformation($"{messagePrefix} Device provisioning start");

            // Do DPS magic first time device seen, ComputeDerivedSymmetricKey seperately so it can be inspected in debugger
            string deviceKey = ComputeDerivedSymmetricKey(Convert.FromBase64String(enrollmentGroupSymmetricKey), registrationID);

            try
            {
               using (var securityProvider = new SecurityProviderSymmetricKey(registrationID, deviceKey, null))
               {
                  using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
                  {
                     ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(globalDeviceEndpoint, scopeID, securityProvider, transport);

                     DeviceRegistrationResult result = await provClient.RegisterAsync();
                     if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                     {
                        throw new ApplicationException($" {messagePrefix} Status:{result.Status} RegisterAsync failed");
                     }
                     log.LogInformation($"{messagePrefix} Status:{result.Status} AssignedHub:{result.AssignedHub}");

                     IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                     deviceClient = DeviceClient.Create(result.AssignedHub, authentication, TransportType.Amqp);

                     if (!DeviceClients.TryUpdate(registrationID, deviceClient, null))
                     {
                        log.LogWarning($" {messagePrefix} TryUpdate failed");
                     }
                  }
               }
            }
            catch (Exception ex)
            {
               if (DeviceClients.TryRemove(registrationID, out deviceClient))
               {
                  log.LogWarning($" {messagePrefix} TryRemove failed");
               }

               log.LogError(ex, $" {messagePrefix} Device provisioning failed");
               throw;
            }
         }

         log.LogInformation($"{messagePrefix} Device provisioning polling start");
         if (!DeviceClients.TryGetValue(registrationID, out deviceClient))
         {
            log.LogError($"{messagePrefix} TryGet while failed");

            throw new ApplicationException($"{messagePrefix} TryGet while failed");
         }

         while (deviceClient == null)
         {
            log.LogInformation($"{messagePrefix} provisioning polling delay");
            await Task.Delay(deviceProvisioningPollingDelay);

            if (!DeviceClients.TryGetValue(registrationID, out deviceClient))
            {
               log.LogError($"{messagePrefix} TryGet while loop failed");

               throw new ApplicationException($"{messagePrefix} TryGet while loopfailed");
            }
         }

         log.LogInformation($"{messagePrefix} Payload assembly start");
         JObject telemetryDataPoint = new JObject();
         try
         {
            JObject payloadFields = (JObject)payloadObect.payload_fields;

            foreach (JProperty child in payloadFields.Children())
            {
               EnumerateChildren(telemetryDataPoint, child);
            }
         }
         catch (Exception ex)
         {
            if (DeviceClients.TryRemove(registrationID, out deviceClient))
            {
               log.LogWarning($"{messagePrefix} TryRemove failed");
            }

            log.LogError(ex, $"{messagePrefix} Payload assembly failed");
            throw;
         }

         log.LogInformation($"{messagePrefix} Payload SendEventAsync start");
         try
         { 
            using (Message ioTHubmessage = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryDataPoint))))
            {
               await deviceClient.SendEventAsync(ioTHubmessage);
            }
         }
         catch (Exception ex)
         {
            if (DeviceClients.TryRemove(registrationID, out deviceClient))
            {
               log.LogWarning($"{messagePrefix} TryRemove failed");
            }

            log.LogError(ex, $"{messagePrefix} SendEventAsync failed");
            throw;
         }

         log.LogInformation($"{messagePrefix} Payload processing completed");
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

      public static string ComputeDerivedSymmetricKey(byte[] masterKey, string registrationId)
      {
         using (var hmac = new HMACSHA256(masterKey))
         {
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(registrationId)));
         }
      }
   }
}