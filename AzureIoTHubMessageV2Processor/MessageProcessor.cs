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
   using System.Globalization;
   using System.Runtime.Caching;
   using System.Security.Cryptography;
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

   public class DeviceContext
   {
      public DeviceClient Uplink { get; set; }
      public Uri Downlink { get; set; }
   }

   public static class MessageProcessor
   {
      static readonly ObjectCache DeviceClients = MemoryCache.Default;
      static readonly ApplicationConfiguration ApplicationConfiguration = new ApplicationConfiguration();

      [FunctionName("UplinkMessageProcessor")]
      public static async Task UplinkRun(
            [QueueTrigger("%UplinkQueueName%", Connection = "AzureStorageConnectionString")]
            PayloadUplink payload,
            ILogger log)
      {
         // Quick n dirty hack to see what difference (if any) not processing retries makes
         if (payload.IsRetry)
         {
            log.LogInformation("DevID:{DeviceId} Counter:{Counter} AppID:{ApplicationId} Uplink message retry", payload.DeviceId, payload.Counter, payload.ApplicationId);
            return;
         }

         log.LogInformation("DevID:{DeviceId} Counter:{Counter} AppID:{ApplicationId} Uplink message device processing start", payload.DeviceId, payload.Counter, payload.ApplicationId);

         ApplicationConfiguration.Initialise();

         string registrationId = ApplicationConfiguration.RegistrationIdResolve(payload.ApplicationId, payload.Port, payload.DeviceId);

         CacheItemPolicy cacheItemPolicy = new CacheItemPolicy()
         {
            SlidingExpiration = new TimeSpan(1, 0, 0, 0),
            //RemovedCallback
         };

         DeviceContext deviceContext = new DeviceContext()
         {
            Uplink = null,
            Downlink = new Uri(payload.DownlinkUrl)
         };

         // See if the device has already been provisioned or is being provisioned on another thread.
         if (DeviceClients.Add(registrationId, deviceContext, cacheItemPolicy))
         {
            log.LogInformation("RegID:{registrationId} Device provisioning start", registrationId);

            try
            {
               // Get DeviceClient if first time device seen
               deviceContext.Uplink = await DeviceRegistration(payload.ApplicationId, payload.DeviceId, payload.Port);
            }
            catch (Exception ex)
            {
               if ( DeviceClients.Remove(registrationId) == null)
               {
                  log.LogWarning("RegID:{registrationID} Device Registration TryRemove failed", registrationId);
               }

               log.LogError(ex, "RegID:{registrationID} Device Registration failed", registrationId);
               throw;
            }

            DeviceClients.Set(registrationId, deviceContext, cacheItemPolicy);

            log.LogInformation("RegID:{registrationId} Assigned to IoTHub", registrationId);
         }

         // Wait for the Device Provisioning Service to complete on this or other thread
         log.LogInformation("RegID:{registrationId} Device provisioning polling start", payload.DeviceId);

         int deviceProvisioningPollingDelay = ApplicationConfiguration.DpsDeviceProvisioningPollingDelay();

         // Wait for the deviceClient to be configured if process kicked off on another thread, no timeout as will get taken care of by function timeout...
         do
         {
            deviceContext = (DeviceContext)DeviceClients.Get(registrationId);
            if (deviceContext == null)
            {
               log.LogError("RegID:{registrationId} DeviceContext provisioning polling Get failed", registrationId);

               throw new ApplicationException($"RegID:{registrationId} DeviceContext provisioning polling Get failed");
            }

            if (deviceContext.Uplink == null)
            {
               log.LogInformation($"RegID:{registrationId} Device provisioning polling delay:{deviceProvisioningPollingDelay}mSec", registrationId, deviceProvisioningPollingDelay);
               await Task.Delay(deviceProvisioningPollingDelay);
            }
         }
         while (deviceContext.Uplink == null);

         log.LogInformation("DevID:{DeviceId} Counter:{counter} Payload Send start", payload.DeviceId, payload.Counter);

         try
         {
            await DeviceTelemetrySend(deviceContext.Uplink, payload);
         }
         catch (Exception ex)
         {
            if (DeviceClients.Remove(registrationId) == null)
            {
               log.LogWarning("DevID:{DeviceId} Counter:{Counter} Payload SendEventAsync TryRemove failed", payload.DeviceId, payload.Counter);
            }

            log.LogError(ex, "DevID:{DeviceId} Counter:{Counter} Telemetry event send failed", payload.DeviceId, payload.Counter);
            throw;
         }

         log.LogInformation("DevID:{DeviceId} Counter:{Counter} Uplink message device processing completed", payload.DeviceId, payload.Counter);
      }

      static async Task<DeviceClient> DeviceRegistration(string applicationId, string deviceId, int port)
      {
         string deviceKey;

         // See if AzureIoT hub connections string has been configured
         string connectionString = ApplicationConfiguration.ConnectionStringResolve(applicationId, port);
         if (!String.IsNullOrEmpty(connectionString))
         {
            return DeviceClient.CreateFromConnectionString(connectionString, deviceId);
         }

         // See if Azure DPS has been configured
         using (var hmac = new HMACSHA256(Convert.FromBase64String(ApplicationConfiguration.DpsEnrollmentGroupSymmetricKeyResolve(applicationId, port))))
         {
            deviceKey = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(deviceId)));
         }

         using (var securityProvider = new SecurityProviderSymmetricKey(deviceId, deviceKey, null))
         {
            using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
            {
               ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(ApplicationConfiguration.DpsGlobaDeviceEndpointResolve(),
                                                                                       ApplicationConfiguration.DpsIdScopeResolve(applicationId, port),
                                                                                       securityProvider,
                                                                                       transport);

               DeviceRegistrationResult result = await provClient.RegisterAsync();
               if (result.Status != ProvisioningRegistrationStatusType.Assigned)
               {
                  throw new ApplicationException($"DevID:{deviceId} Status:{result.Status} RegisterAsync failed");
               }

               IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

               return DeviceClient.Create(result.AssignedHub,
                  authentication,
                  new ITransportSettings[]
                  {
                     new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                     {
                        PrefetchCount = 0,
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                        {
                           Pooling = true,
                        }
                     }
                  }
               );
            }
         }
      }

      static async Task DeviceTelemetrySend(DeviceClient deviceClient, PayloadUplink payloadObject)
      {
         JObject telemetryEvent = new JObject();

         telemetryEvent.Add("DeviceEUI", payloadObject.DeviceEui);
         telemetryEvent.Add("Retry", payloadObject.IsRetry);
         telemetryEvent.Add("Counter", payloadObject.Counter);
         telemetryEvent.Add("DeviceID", payloadObject.DeviceId);
         telemetryEvent.Add("ApplicationID", payloadObject.ApplicationId);
         telemetryEvent.Add("Port", payloadObject.Port);
         telemetryEvent.Add("PayloadRaw", payloadObject.PayloadRaw);
         telemetryEvent.Add("ReceivedAtUTC", payloadObject.Metadata.ReceivedAtUtc);

         // If the payload has been unpacked in TTN backend add fields to telemetry event payload
         if (payloadObject.PayloadFields != null)
         {
            EnumerateChildren(telemetryEvent, payloadObject.PayloadFields);
         }

         // Send the message to Azure IoT Hub/Azure IoT Central
         using (Message ioTHubmessage = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryEvent))))
         {
            // Ensure the displayed time is the acquired time rather than the uploaded time. esp. important for when messages that ended up 
            // in poison queue are returned to the processing queue. 
            ioTHubmessage.Properties.Add("iothub-creation-time-utc", payloadObject.Metadata.ReceivedAtUtc.ToString("s", CultureInfo.InvariantCulture));
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
