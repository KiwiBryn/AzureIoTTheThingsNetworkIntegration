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
namespace devMobile.TheThingsNetwork.AzureIoTHubMessageProcessor
{
   using System;
   using System.Collections.Concurrent;
   using System.Security.Cryptography;
   using System.Globalization;
   using System.Text;
   using System.Threading.Tasks;

   using Microsoft.Azure.Devices.Client;
   using Microsoft.Azure.Devices.Provisioning.Client;
   using Microsoft.Azure.Devices.Provisioning.Client.Transport;
   using Microsoft.Azure.Devices.Shared;
   using Microsoft.Azure.WebJobs;
   using Microsoft.Extensions.Logging;

   using Newtonsoft.Json;
   using Newtonsoft.Json.Linq;
      
   using devMobile.TheThingsNetwork.MessageProcessor.Models;

   public static class MessageProcessor
   {
      static readonly ConcurrentDictionary<string, DeviceClient> DeviceClients = new ConcurrentDictionary<string, DeviceClient>();
      static readonly ApplicationConfiguration ApplicationConfiguration = new ApplicationConfiguration();

      [FunctionName("UplinkMessageProcessor")]
      public static async Task UplinkRun(
         [QueueTrigger("%UplinkQueueName%", Connection = "AzureStorageConnectionString")]
            PayloadUplink payload,
            ILogger log)
      {
         DeviceClient deviceClient = null;

         // Quick n dirty hack to see what difference (if any) not processing retries makes
         if (payload.is_retry)
         {
            log.LogInformation("DevID:{dev_id} Counter:{counter} AppID:{app_id} Uplink message retry", payload.dev_id, payload.counter, payload.app_id);
            return;
         }

         log.LogInformation("DevID:{dev_id} Counter:{counter} AppID:{app_id} Uplink message device processing start", payload.dev_id, payload.counter, payload.app_id);

         ApplicationConfiguration.Initialise();

         string registrationId = ApplicationConfiguration.RegistrationIdResolve(payload.app_id, payload.port, payload.dev_id);

         // See if the device has already been provisioned or is being provisioned on another thread.
         if (DeviceClients.TryAdd(registrationId, deviceClient))
         {
            log.LogInformation("RegID:{registrationId} Device provisioning start", registrationId);

            try
            {
               // Do DPS magic first time device seen
               deviceClient = await DeviceRegistration(
                  ApplicationConfiguration.DpsGlobaDeviceEndpointResolve(),
                  ApplicationConfiguration.DpsIdScopeResolve(payload.app_id, payload.port),
                  ApplicationConfiguration.DpsEnrollmentGroupSymmetricKeyResolve(payload.app_id, payload.port), 
                  payload.dev_id);
            }
            catch (Exception ex)
            {
               if (!DeviceClients.TryRemove(registrationId, out deviceClient))
               {
                  log.LogWarning("RegID:{registrationID} Device Registration TryRemove failed", registrationId);
               }

               log.LogError(ex, "RegID:{registrationID} Device Registration failed", registrationId);
               throw;
            }

            if (!DeviceClients.TryUpdate(registrationId, deviceClient, null))
            {
               log.LogWarning("RegID:{registrationID} Device Registration TryUpdate failed", registrationId);
            }

            log.LogInformation("RegID:{registrationId} Assigned to IoTHub", registrationId );
         }

         // Wait for the Device Provisioning Service to complete on this or other thread
         log.LogInformation("RegID:{registrationId} Device provisioning polling start", payload.dev_id);

         int deviceProvisioningPollingDelay = ApplicationConfiguration.DpsDeviceProvisioningPollingDelay();

         // Wait for the deviceClient to be configured if process kicked off on another thread, no timeout as will get taken care of by function timeout...
         do
         {
            if (!DeviceClients.TryGetValue(registrationId, out deviceClient))
            {
               log.LogWarning("RegID:{registrationId} Device provisioning polling TryGet do while loop failed", registrationId);

               throw new ApplicationException($"RegID:{registrationId} Device provisioning polling TryGet do while loop failed");
            }

            if (deviceClient == null)
            {
               log.LogInformation($"DevID:{registrationId} Device provisioning polling delay:{deviceProvisioningPollingDelay}mSec", registrationId, deviceProvisioningPollingDelay);
               await Task.Delay(deviceProvisioningPollingDelay);
            }
         }
         while (deviceClient == null);

         log.LogInformation("DevID:{dev_id} Counter:{counter} Payload Send start", payload.dev_id, payload.counter);

         try
         {
            await DeviceTelemetrySend(deviceClient, payload);
         }
         catch (Exception ex)
         {
            if (!DeviceClients.TryRemove(registrationId, out deviceClient))
            {
               log.LogWarning("DevID:{dev_id} Counter:{counter} Payload SendEventAsync TryRemove failed", payload.dev_id, payload.counter);
            }

            log.LogError(ex, "DevID:{dev_id} Counter:{counter} Telemetry event send failed", payload.dev_id, payload.counter);
            throw;
         }

         log.LogInformation("DevID:{deviceId} Counter:{counter} Uplink message device processing completed", payload.dev_id, payload.counter);
      }

      static async Task<DeviceClient> DeviceRegistration(string globalDeviceEndpoint, string IdScope, string enrollmentGroupSymmetricKey, string deviceId)
      {
         DeviceClient deviceClient;
         string deviceKey;

         // Compute the derived symmetric key for the DeviceId not the registrationId
         using (var hmac = new HMACSHA256(Convert.FromBase64String(enrollmentGroupSymmetricKey)))
         {
            deviceKey = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(deviceId)));
         }

         using (var securityProvider = new SecurityProviderSymmetricKey(deviceId, deviceKey, null))
         {
            using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
            {
               ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(globalDeviceEndpoint, IdScope, securityProvider, transport);

               DeviceRegistrationResult result = await provClient.RegisterAsync();
               if (result.Status != ProvisioningRegistrationStatusType.Assigned)
               {
                  throw new ApplicationException($"DevID:{deviceId} Status:{result.Status} RegisterAsync failed");
               }

               IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

               deviceClient = DeviceClient.Create(result.AssignedHub, authentication, TransportType.Amqp);
            }
         }
        
         return deviceClient;
      }

      static async Task DeviceTelemetrySend(DeviceClient deviceClient, PayloadUplink payloadObject)
      {
         JObject telemetryEvent = new JObject();

         JObject payloadFields = (JObject)payloadObject.payload_fields;
         telemetryEvent.Add("HardwareSerial", payloadObject.hardware_serial);
         telemetryEvent.Add("Retry", payloadObject.is_retry);
         telemetryEvent.Add("Counter", payloadObject.counter);
         telemetryEvent.Add("DeviceID", payloadObject.dev_id);
         telemetryEvent.Add("ApplicationID", payloadObject.app_id);
         telemetryEvent.Add("Port", payloadObject.port);
         telemetryEvent.Add("PayloadRaw", payloadObject.payload_raw);
         telemetryEvent.Add("ReceivedAtUTC", payloadObject.metadata.time);

         // If the payload has been unpacked in TTN backend add fields to telemetry event payload
         if (payloadFields != null)
         {
            foreach (JProperty child in payloadFields.Children())
            {
               EnumerateChildren(telemetryEvent, child);
            }
         }

         // Send the message to Azure IoT Hub/Azure IoT Central
         using (Message ioTHubmessage = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryEvent))))
         {
            // Ensure the displayed time is the acquired time rather than the uploaded time. esp. important for when messages that ended up 
            // in poison queue are returned to the processing queue. 
            ioTHubmessage.Properties.Add("iothub-creation-time-utc", payloadObject.metadata.time.ToString("s", CultureInfo.InvariantCulture));
            await deviceClient.SendEventAsync(ioTHubmessage);
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

      [FunctionName("DownlinkMessageProcessor")]
      public static async Task DownlinkRun(
      [QueueTrigger("%DownlinkQueueName%", Connection = "AzureStorageConnectionString")]
         PayloadDownlink payload,
         ILogger log)
      {
      }
   }
}
