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
namespace devMobile.TheThingsNetwork.AzureIoTHubUplinkMessageProcessor
{
   using System;
   using System.Collections.Concurrent;
   using System.Collections.Generic;
   using System.Security.Cryptography;
   using System.Globalization;
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

   using devMobile.TheThingsNetwork.AzureIoTHubUplinkMessageProcessor.Models;

   public static class UplinkMessageProcessor
   {
      static readonly ConcurrentDictionary<string, DeviceClient> DeviceClients = new ConcurrentDictionary<string, DeviceClient>();

      [FunctionName("UplinkMessageProcessor")]
      //[Singleton] // used when debugging
      public static async Task Run(
         [QueueTrigger("%UplinkQueueName%", Connection = "AzureStorageConnectionString")]
         CloudQueueMessage cloudQueueMessage, // Used to get CloudQueueMessage.Id for logging
         ExecutionContext context,
         ILogger log)
      {
         PayloadV5 payloadObect; // need to refactor and decorate Payload classes
         DeviceClient deviceClient = null;
         DeviceProvisioningServiceSettings deviceProvisioningServiceConfig;

         string environmentName = Environment.GetEnvironmentVariable("ENVIRONMENT");
         if (string.IsNullOrEmpty(environmentName))
         {
            log.LogWarning( $"ENVIRONMENT variable not set using appsettings.json");
         }

         // Load configuration for DPS. need to refactor approach and store securely...
         var configuration = new ConfigurationBuilder()
         .SetBasePath(context.FunctionAppDirectory)
         .AddJsonFile($"appsettings.json")
         .AddJsonFile($"appsettings.{environmentName}.json")
         .AddEnvironmentVariables()
         .Build();

         // Load configuration for DPS. Refactor approach and store securely...
         try
         {
            deviceProvisioningServiceConfig = (DeviceProvisioningServiceSettings)configuration.GetSection("DeviceProvisioningService").Get<DeviceProvisioningServiceSettings>();
         }
         catch (Exception ex)
         {
            log.LogError(ex, $"Configuration loading failed");
            throw;
         }

         // Deserialise uplink message from Azure storage queue
         try
         {
            payloadObect = JsonConvert.DeserializeObject<PayloadV5>(cloudQueueMessage.AsString);
         }
         catch (Exception ex)
         {
            log.LogError(ex, $"MessageID:{cloudQueueMessage.Id} uplink message deserialisation failed");
            throw;
         }

         // Extract the device ID as it's used lots of places
         string registrationID = payloadObect.dev_id;

         // Construct the prefix used in all the logging
         string messagePrefix = $"MessageID: {cloudQueueMessage.Id} DeviceID:{registrationID} Counter:{payloadObect.counter} Application ID:{payloadObect.app_id}";
         log.LogInformation($"{messagePrefix} Uplink message device processing start");

         deviceClient = await DeviceCreate(log, messagePrefix, deviceProvisioningServiceConfig, payloadObect.app_id, payloadObect.dev_id);

         await DeviceTelemetrySend(log, messagePrefix, deviceClient, payloadObect);

         log.LogInformation($"{messagePrefix} Uplink message device processing completed");
      }

      static async Task<DeviceClient> DeviceCreate(ILogger log, string messagePrefix, DeviceProvisioningServiceSettings deviceProvisioningServiceConfig, string applicationId, string registrationID )
      {
         DeviceClient deviceClient = null;

         // See if the device has already been provisioned on another thread.
         if (DeviceClients.TryAdd(registrationID, deviceClient))
         {
            log.LogInformation($"{messagePrefix} Device provisioning start");

            string enrollmentGroupSymmetricKey = deviceProvisioningServiceConfig.EnrollmentGroupSymmetricKeyDefault;

            // figure out if custom mapping for TTN applicationID
            if (deviceProvisioningServiceConfig.ApplicationEnrollmentGroupMapping != null)
            {
               deviceProvisioningServiceConfig.ApplicationEnrollmentGroupMapping.GetValueOrDefault(applicationId, deviceProvisioningServiceConfig.EnrollmentGroupSymmetricKeyDefault);
            }

            // Do DPS magic first time device seen
            await DeviceRegistration(log, messagePrefix, deviceProvisioningServiceConfig.GlobalDeviceEndpoint, deviceProvisioningServiceConfig.IdScope, enrollmentGroupSymmetricKey, registrationID);
         }

         // Wait for the Device Provisioning Service to complete on this or other thread
         log.LogInformation($"{messagePrefix} Device provisioning polling start");
         if (!DeviceClients.TryGetValue(registrationID, out deviceClient))
         {
            log.LogError($"{messagePrefix} Device provisioning polling TryGet before while failed");

            throw new ApplicationException($"{messagePrefix} Device provisioning polling TryGet before while failed");
         }

         while (deviceClient == null)
         {
            log.LogInformation($"{messagePrefix} provisioning polling delay");
            await Task.Delay(deviceProvisioningServiceConfig.DeviceProvisioningPollingDelay);

            if (!DeviceClients.TryGetValue(registrationID, out deviceClient))
            {
               log.LogError($"{messagePrefix} Device provisioning polling TryGet while loop failed");

               throw new ApplicationException($"{messagePrefix} Device provisioning polling TryGet while loopfailed");
            }
         }
         return deviceClient;
      }

      static async Task DeviceTelemetrySend(ILogger log, string messagePrefix, DeviceClient deviceClient, PayloadV5 payloadObect )
      {
         // Assemble the JSON payload to send to Azure IoT Hub/Central.
         log.LogInformation($"{messagePrefix} Payload assembly start");
         JObject telemetryEvent = new JObject();
         try
         {
            JObject payloadFields = (JObject)payloadObect.payload_fields;
            telemetryEvent.Add("HardwareSerial", payloadObect.hardware_serial);
            telemetryEvent.Add("Retry", payloadObect.is_retry);
            telemetryEvent.Add("Counter", payloadObect.counter);
            telemetryEvent.Add("DeviceID", payloadObect.dev_id);
            telemetryEvent.Add("ApplicationID", payloadObect.app_id);
            telemetryEvent.Add("Port", payloadObect.port);
            telemetryEvent.Add("PayloadRaw", payloadObect.payload_raw);
            telemetryEvent.Add("ReceivedAtUTC", payloadObect.metadata.time);

            // If the payload has been unpacked in TTN backend add fields to telemetry event payload
            if (payloadFields != null)
            {
               foreach (JProperty child in payloadFields.Children())
               {
                  EnumerateChildren(telemetryEvent, child);
               }
            }
         }
         catch (Exception ex)
         {
            log.LogError(ex, $"{messagePrefix} Payload processing or Telemetry event assembly failed");
            throw;
         }

         // Send the message to Azure IoT Hub/Azure IoT Central
         log.LogInformation($"{messagePrefix} Payload SendEventAsync start");
         try
         {
            using (Message ioTHubmessage = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryEvent))))
            {
               // Ensure the displayed time is the acquired time rather than the uploaded time. esp. important for when messages that ended up 
               // in poison queue are returned to the processing queue. 
               ioTHubmessage.Properties.Add("iothub-creation-time-utc", payloadObect.metadata.time.ToString("s", CultureInfo.InvariantCulture));
               await deviceClient.SendEventAsync(ioTHubmessage);
            }
         }
         catch (Exception ex)
         {
            if (!DeviceClients.TryRemove(payloadObect.dev_id, out deviceClient))
            {
               log.LogWarning($"{messagePrefix} TryRemove SendEventAsync failed");
            }

            log.LogError(ex, $"{messagePrefix} SendEventAsync failed");
            throw;
         }

      }

      static async Task DeviceRegistration(ILogger log, string messagePrefix, string globalDeviceEndpoint, string IdScope, string enrollmentGroupSymmetricKey, string registrationId)
      {
         DeviceClient deviceClient;

         try
         {
            string deviceKey = ComputeDerivedSymmetricKey(Convert.FromBase64String(enrollmentGroupSymmetricKey), registrationId);

            using (var securityProvider = new SecurityProviderSymmetricKey(registrationId, deviceKey, null))
            {
               using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
               {
                  ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(globalDeviceEndpoint, IdScope, securityProvider, transport);

                  DeviceRegistrationResult result = await provClient.RegisterAsync();
                  if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                  {
                     throw new ApplicationException($"{messagePrefix} Status:{result.Status} RegisterAsync failed");
                  }
                  log.LogInformation($"{messagePrefix} Device provisioned Status:{result.Status} AssignedHub:{result.AssignedHub}");

                  IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                  deviceClient = DeviceClient.Create(result.AssignedHub, authentication, TransportType.Amqp);

                  if (!DeviceClients.TryUpdate(registrationId, deviceClient, null))
                  {
                     log.LogWarning($"{messagePrefix} Device provisoning TryUpdate failed");
                  }
               }
            }
         }
         catch (Exception ex)
         {
            if (!DeviceClients.TryRemove(registrationId, out deviceClient))
            {
               log.LogWarning($"{messagePrefix} Device provisoning TryRemove failed");
            }

            log.LogError(ex, $"{messagePrefix} Device provisioning failed");
            throw;
         }
      }

      static void EnumerateChildren(JObject jobject, JToken token)
      {
         if (token is JProperty property)
         {
            if (token.First is JValue)
            {
               // Temporary dirty hack for Azure IoT Central compatibility
               if (token.Parent is JObject possibleGpsProperty)
               {
                  if (possibleGpsProperty.Path.StartsWith("GPS", StringComparison.OrdinalIgnoreCase))
                  {
                     if (string.Compare(property.Name, "Latitude", true) == 0)
                     {
                        jobject.Add("lat", property.Value);
                     }
                     if (string.Compare(property.Name, "Longitude", true) == 0)
                     {
                        jobject.Add("lon", property.Value);
                     }
                     if (string.Compare(property.Name, "Altitude", true) == 0)
                     {
                        jobject.Add("alt", property.Value);
                     }
                  }
               }
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