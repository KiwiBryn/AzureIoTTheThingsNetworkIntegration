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

   using devMobile.TheThingsNetwork.AzureIoTHubUplinkMessageProcessor.Models;

   public static class UplinkMessageProcessor
   {
      static readonly ConcurrentDictionary<string, DeviceClient> DeviceClients = new ConcurrentDictionary<string, DeviceClient>();
      static IConfiguration Configuration = null;
      static ILogger Log = null;

      [FunctionName("UplinkMessageProcessor")]
      public static async Task Run(
         [QueueTrigger("%UplinkQueueName%", Connection = "AzureStorageConnectionString")]
            PayloadV5 payloadObject,
            ILogger log)
      {
         DeviceClient deviceClient = null;

         // I worry about threading for this and configuration 
         if ( Log == null)
         {
            Log = log;
         }

         // Quick n dirty hack to see what difference (if any) not processing retries makes
         if (payloadObject.is_retry)
         {
            Log.LogInformation("DevID:{dev_id} AppID:{app_id} Counter:{counter} Uplink message retry", payloadObject.dev_id, payloadObject.app_id, payloadObject.counter);
            return;
         }

         // Check that KeyVault URI is configured in environment variables. Not a lot we can do if it isn't....
         if (Configuration == null)
         {
            string keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultURI");
            if (string.IsNullOrEmpty(keyVaultUri))
            {
               Log.LogError("KeyVaultURI environment variable not set");
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
               Log.LogError(ex, $"Configuration loading failed");
               throw;
            }
         }

         Log.LogInformation("DevID:{dev_id} AppID:{app_id} Counter:{counter} Uplink message device processing start", payloadObject.dev_id, payloadObject.app_id, payloadObject.counter);

         deviceClient = await DeviceClientCreate(
            Configuration.GetSection("DPSGlobaDeviceEndpoint").Value,
            Configuration.GetSection("DPSIDScope").Value,
            payloadObject.app_id, 
            payloadObject.dev_id);

         await DeviceTelemetrySend(deviceClient, payloadObject);

         Log.LogInformation("DevID:{dev_id} AppID:{app_id} Counter:{counter} Uplink message device processing completed", payloadObject.dev_id, payloadObject.app_id, payloadObject.counter);
      }

      static async Task<DeviceClient> DeviceClientCreate(
         string globalDeviceEndpoint,
         string idScope,
         string applicationId, 
         string deviceId )
      {
         DeviceClient deviceClient = null;

         // See if the device has already been provisioned or is being provisioned on another thread.
         if (DeviceClients.TryAdd(deviceId, deviceClient))
         {
            Log.LogInformation("DevID:{deviceId} AppID:{applicationId} Device provisioning start", deviceId, applicationId);

            // Check to see if there is application specific configuration, otherwise run with default
            string enrollmentGroupSymmetricKey = Configuration.GetSection($"DPSEnrollmentGroupSymmetricKey-{applicationId}").Value;
            if (enrollmentGroupSymmetricKey == null)
            {
               enrollmentGroupSymmetricKey = Configuration.GetSection("DPSEnrollmentGroupSymmetricKeyDefault").Value;
            }

            // Do DPS magic first time device seen
            await DeviceRegistration(globalDeviceEndpoint, idScope, enrollmentGroupSymmetricKey, deviceId, applicationId);
         }

         // Wait for the Device Provisioning Service to complete on this or other thread
         Log.LogInformation("DevID:{deviceId} AppID:{applicationId} Device provisioning polling start", deviceId, applicationId);

         if (!DeviceClients.TryGetValue(deviceId, out deviceClient))
         {
            Log.LogWarning("DevID:{deviceId} AppID:{applicationId} Device provisioning polling TryGet before while failed", deviceId, applicationId);

            throw new ApplicationException($"DevID:{deviceId} AppID:{applicationId} Device provisioning polling TryGet before while failed");
         }

         int deviceProvisioningPollingDelay = int.Parse(Configuration.GetSection("DeviceProvisioningPollingDelay").Value);

         // Wait for the deviceClient to be configured if process kicked off on another thread, not timeout as will get 
         // taken care of by function timeout...
         do
         {
            if (!DeviceClients.TryGetValue(deviceId, out deviceClient))
            {
               Log.LogWarning("DevID:{deviceId} AppID:{applicationId} Device provisioning polling TryGet do while loop failed", deviceId, applicationId);

               throw new ApplicationException($"DevID:{deviceId} AppID:{applicationId} Device provisioning polling TryGet do while loop failed");
            }

            if (deviceClient== null)
            {
               Log.LogInformation($"DevID:{deviceId} AppID:{applicationId} provisioning polling delay:{deviceProvisioningPollingDelay}mSec", deviceId, applicationId, deviceProvisioningPollingDelay);
               await Task.Delay(deviceProvisioningPollingDelay);
            }
         }
         while (deviceClient == null);

         return deviceClient;
      }

      static async Task DeviceTelemetrySend(DeviceClient deviceClient, PayloadV5 payloadObject )
      {
         // Assemble the JSON payload to send to Azure IoT Hub/Central.
         Log.LogInformation("DevID:{dev_id} AppID:{app_id} Payload assembly start", payloadObject.dev_id, payloadObject.app_id);

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
            Log.LogError(ex, "DevID:{dev_id} AppID:{app_id} Payload processing or Telemetry event assembly failed", payloadObject.dev_id, payloadObject.app_id);
            throw;
         }

         // Send the message to Azure IoT Hub/Azure IoT Central
         Log.LogInformation("DevID:{dev_id} AppID:{app_id} Payload SendEventAsync start", payloadObject.dev_id, payloadObject.app_id);
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
               Log.LogWarning("DevID:{dev_id} AppID:{app_id} Payload SendEventAsync TryRemove failed", payloadObject.dev_id, payloadObject.app_id);
            }

            Log.LogError(ex, "DevID:{dev_id} AppID:{app_id} SendEventAsync failed", payloadObject.dev_id, payloadObject.app_id);
            throw;
         }
      }

      static async Task DeviceRegistration(string globalDeviceEndpoint, string IdScope, string enrollmentGroupSymmetricKey, string registrationId, string applicationId)
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
                     throw new ApplicationException($"DevID:{registrationId} AppID:{applicationId} Status:{result.Status} RegisterAsync failed");
                  }
                  Log.LogInformation("DevID:{registrationId} AppID:{applicationId} Assigned IoTHub:{assignedHub}", registrationId, applicationId, result.AssignedHub);

                  IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                  deviceClient = DeviceClient.Create(result.AssignedHub, authentication, TransportType.Amqp);

                  if (!DeviceClients.TryUpdate(registrationId, deviceClient, null))
                  {
                     Log.LogWarning("DevID:{registrationID} AppID:{applicationId} Device Registration TryUpdate failed", registrationId, applicationId);
                  }
               }
            }
         }
         catch (Exception ex)
         {
            if (!DeviceClients.TryRemove(registrationId, out deviceClient))
            {
               Log.LogWarning("DevID:{registrationID} AppID:{applicationId} Device Registration TryRemove failed", registrationId, applicationId);
            }

            Log.LogError(ex, "DevID:{registrationID} AppID:{applicationId} Device Registration failed", registrationId, applicationId);
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
