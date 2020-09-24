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
namespace devMobile.TheThingsNetwork.AzureIoTMessageProcessor
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
   using Microsoft.Extensions.Configuration;
   using Microsoft.Extensions.Logging;

   using Newtonsoft.Json;
   using Newtonsoft.Json.Linq;
      
   using devMobile.TheThingsNetwork.MessageProcessor.Models;

   public static class MessageProcessor
   {
      const string DpsGlobaDeviceEndpointDefault = "global.azure-devices-provisioning.net";
      static readonly ConcurrentDictionary<string, DeviceClient> DeviceClients = new ConcurrentDictionary<string, DeviceClient>();
      static IConfiguration Configuration = null;

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
            log.LogInformation("DevID:{dev_id} AppID:{app_id} Counter:{counter} Uplink message retry", payload.dev_id, payload.app_id, payload.counter);
            return;
         }

         log.LogInformation("DevID:{dev_id} AppID:{app_id} Counter:{counter} Uplink message device processing start", payload.dev_id, payload.app_id, payload.counter);

         // Check that KeyVault URI is configured in environment variables. Not a lot we can do if it isn't....
         if (Configuration == null)
         {
            string keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultURI");
            if (string.IsNullOrEmpty(keyVaultUri))
            {
               log.LogError("KeyVaultURI environment variable not set");
               throw new ApplicationException("KeyVaultURI environment variable not set");
            }

            // Load configuration from KeyVault 
            try
            {
               Configuration = new ConfigurationBuilder()
                  .AddEnvironmentVariables()
                  .AddAzureKeyVault(keyVaultUri)
                  .Build();
            }
            catch (Exception ex)
            {
               log.LogError(ex, $"Configuration loading failed");
               throw;
            }
         }

         string dpsGlobaDeviceEndpoint = DpsGlobaDeviceEndpointResolve(Configuration);
         string dpsIdScope = DpsIdScopeResolve(Configuration, payload.app_id, payload.port);
         string dpsEnrollmentGroupSymmetricKey = DpsEnrollmentGroupSymmetricKeyResolve(Configuration, payload.app_id, payload.port);
         string registrationId = RegistrationIdResolve(Configuration, payload.app_id, payload.port, payload.dev_id);
         int deviceProvisioningPollingDelay = DpsDeviceProvisioningPollingDelay(Configuration);

         // See if the device has already been provisioned or is being provisioned on another thread.
         if (DeviceClients.TryAdd(registrationId, deviceClient))
         {
            log.LogInformation("RegID:{registrationId} Device provisioning start", payload.dev_id);

            try
            {
               // Do DPS magic first time device seen
               deviceClient = await DeviceRegistration(dpsGlobaDeviceEndpoint, dpsIdScope, dpsEnrollmentGroupSymmetricKey, registrationId);
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
               log.LogWarning("DevID:{registrationID} Device Registration TryUpdate failed", registrationId);
            }
            //Log.LogInformation("DevID:{registrationId} Assigned IoTHub:{assignedHub}", registrationId, deviceClient);
            log.LogInformation("DevID:{registrationId} Assigned IoTHub", registrationId );
         }

         // Wait for the Device Provisioning Service to complete on this or other thread
         log.LogInformation("RegID:{registrationId} Device provisioning polling start", registrationId);

         // Wait for the deviceClient to be configured if process kicked off on another thread, not timeout as will get taken care of by function timeout...
         do
         {
            if (!DeviceClients.TryGetValue(registrationId, out deviceClient))
            {
               log.LogWarning("RegID:{registrationId} Device provisioning polling TryGet do while loop failed", registrationId);

               throw new ApplicationException($"RegID:{registrationId} Device provisioning polling TryGet do while loop failed");
            }

            if (deviceClient == null)
            {
               log.LogInformation($"RegID:{registrationId} Device provisioning polling delay:{deviceProvisioningPollingDelay}mSec", registrationId, deviceProvisioningPollingDelay);
               await Task.Delay(deviceProvisioningPollingDelay);
            }
         }
         while (deviceClient == null);

         await DeviceTelemetrySend(log, deviceClient, payload);

         log.LogInformation("DevID:{deviceId} AppID:{app_id} Counter:{counter} Uplink message device processing completed", payload.dev_id, payload.app_id, payload.counter);
      }

      static string DpsGlobaDeviceEndpointResolve(IConfiguration configuration)
      {
         string dpsGlobaDeviceEndpoint = configuration.GetSection("DPSGlobaDeviceEndpoint").Value;
         if (string.IsNullOrEmpty(dpsGlobaDeviceEndpoint))
         {
            dpsGlobaDeviceEndpoint = DpsGlobaDeviceEndpointDefault;
         }

         return dpsGlobaDeviceEndpoint;
      }

      static string DpsIdScopeResolve(IConfiguration configuration, string applicationId, int port)
      {
         // Check to see if there is application specific configuration, otherwise run with default
         string idScope = configuration.GetSection($"DPSIDScope-{applicationId}").Value;
         if (idScope == null)
         {
            idScope = configuration.GetSection("DPSIDScopeDefault").Value;
         }

         return idScope;
      }

      static string DpsEnrollmentGroupSymmetricKeyResolve(IConfiguration configuration, string applicationId, int port)
      {
         // Check to see if there is application specific configuration, otherwise run with default
         string enrollmentGroupSymmetricKey = configuration.GetSection($"DPSEnrollmentGroupSymmetricKey-{applicationId}").Value;
         if (enrollmentGroupSymmetricKey == null)
         {
            enrollmentGroupSymmetricKey = configuration.GetSection("DPSEnrollmentGroupSymmetricKeyDefault").Value;
         }

         return enrollmentGroupSymmetricKey;
      }

      static int DpsDeviceProvisioningPollingDelay(IConfiguration configuration)
      {
         int deviceProvisioningPollingDelay;

         deviceProvisioningPollingDelay = int.Parse(configuration.GetSection("DeviceProvisioningPollingDelay").Value);

         return deviceProvisioningPollingDelay;
      }

      static string RegistrationIdResolve(IConfiguration configuration, string applicationId, int port, string deviceId)
      {
         return deviceId;
      }

      static async Task<DeviceClient> DeviceRegistration(string globalDeviceEndpoint, string IdScope, string enrollmentGroupSymmetricKey, string registrationId)
      {
         DeviceClient deviceClient;

            string deviceKey = ComputeDerivedSymmetricKey(Convert.FromBase64String(enrollmentGroupSymmetricKey), registrationId);

            using (var securityProvider = new SecurityProviderSymmetricKey(registrationId, deviceKey, null))
            {
               using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
               {
                  ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(globalDeviceEndpoint, IdScope, securityProvider, transport);

                  DeviceRegistrationResult result = await provClient.RegisterAsync();
                  if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                  {
                     throw new ApplicationException($"DevID:{registrationId} Status:{result.Status} RegisterAsync failed");
                  }

                  IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                  deviceClient = DeviceClient.Create(result.AssignedHub, authentication, TransportType.Amqp);
               }
            }
        
         return deviceClient;
      }

      static async Task DeviceTelemetrySend(ILogger log, DeviceClient deviceClient, PayloadUplink payloadObject)
      {
         // Assemble the JSON payload to send to Azure IoT Hub/Central.
         log.LogInformation("DevID:{dev_id} AppID:{app_id} Payload assembly start", payloadObject.dev_id, payloadObject.app_id);

         JObject telemetryEvent = new JObject();
         try
         {
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
         }
         catch (Exception ex)
         {
            log.LogError(ex, "DevID:{dev_id} AppID:{app_id} Payload processing or Telemetry event assembly failed", payloadObject.dev_id, payloadObject.app_id);
            throw;
         }

         // Send the message to Azure IoT Hub/Azure IoT Central
         log.LogInformation("DevID:{dev_id} AppID:{app_id} Payload SendEventAsync start", payloadObject.dev_id, payloadObject.app_id);
         try
         {
            using (Message ioTHubmessage = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryEvent))))
            {
               // Ensure the displayed time is the acquired time rather than the uploaded time. esp. important for when messages that ended up 
               // in poison queue are returned to the processing queue. 
               ioTHubmessage.Properties.Add("iothub-creation-time-utc", payloadObject.metadata.time.ToString("s", CultureInfo.InvariantCulture));
               await deviceClient.SendEventAsync(ioTHubmessage);
            }
         }
         catch (Exception ex)
         {
            if (!DeviceClients.TryRemove(payloadObject.dev_id, out deviceClient))
            {
               log.LogWarning("DevID:{dev_id} AppID:{app_id} Payload SendEventAsync TryRemove failed", payloadObject.dev_id, payloadObject.app_id);
            }

            log.LogError(ex, "DevID:{dev_id} AppID:{app_id} SendEventAsync failed", payloadObject.dev_id, payloadObject.app_id);
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

      [FunctionName("DownlinkMessageProcessor")]
      public static async Task DownlinkRun(
      [QueueTrigger("%DownlinkQueueName%", Connection = "AzureStorageConnectionString")]
         PayloadDownlink payload,
         ILogger log)
      {
      }
   }
}
