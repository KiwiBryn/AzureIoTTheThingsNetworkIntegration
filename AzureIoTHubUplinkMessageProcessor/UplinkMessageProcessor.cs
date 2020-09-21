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
            ExecutionContext context,
            ILogger log)
      {
         DeviceClient deviceClient = null;

         // I worry about threading for this and configuration 
         if ( Log == null)
         {
            Log = log;
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
                  .SetBasePath(context.FunctionAppDirectory)
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

         Log.LogInformation("DevID:{registrationID} AppID:{applicationId} Uplink message device processing start", payloadObject.dev_id, payloadObject.app_id);

         deviceClient = await DeviceCreate(
            Configuration.GetSection("DPSGlobaDeviceEndpoint").Value,
            Configuration.GetSection("DPSIDScope").Value,
            payloadObject.app_id, 
            payloadObject.dev_id);

         await DeviceTelemetrySend(deviceClient, payloadObject);

         Log.LogInformation("DevID:{registrationID} AppID:{applicationId} Uplink message device processing completed", payloadObject.dev_id, payloadObject.app_id);
      }

      static async Task<DeviceClient> DeviceCreate(
         string globalDeviceEndpoint,
         string idScope,
         string applicationId, 
         string registrationID )
      {
         DeviceClient deviceClient = null;

         // See if the device has already been provisioned on another thread.
         if (DeviceClients.TryAdd(registrationID, deviceClient))
         {
            Log.LogInformation("DevID:{registrationID} AppID:{applicationId} Device provisioning start", registrationID, applicationId);

            string enrollmentGroupSymmetricKey = Configuration.GetSection("DPSEnrollmentGroupSymmetricKeyDefault").Value;

            string enrollmentGroupSymmetricKeyApplication = Configuration.GetSection($"DPSEnrollmentGroupSymmetricKey-{applicationId}").Value;
            if (enrollmentGroupSymmetricKeyApplication != null)
            {
               enrollmentGroupSymmetricKey = enrollmentGroupSymmetricKeyApplication;
            }

            // Do DPS magic first time device seen
            await DeviceRegistration(globalDeviceEndpoint, idScope, enrollmentGroupSymmetricKey, registrationID, applicationId);
         }

         // Wait for the Device Provisioning Service to complete on this or other thread
         Log.LogInformation("DevID:{registrationID} AppID:{applicationId} Device provisioning polling start", registrationID, applicationId);

         if (!DeviceClients.TryGetValue(registrationID, out deviceClient))
         {
            Log.LogWarning("DevID:{registrationID} AppID:{applicationId} Device provisioning polling TryGet before while failed", registrationID, applicationId);

            throw new ApplicationException($"DevID:{registrationID} AppID:{applicationId} Device provisioning polling TryGet before while failed");
         }

         while (deviceClient == null)
         {
            Log.LogInformation($"DevID:{registrationID} AppID:{applicationId} provisioning polling delay", registrationID, applicationId);
            //await Task.Delay(deviceProvisioningServiceConfig.DeviceProvisioningPollingDelay);
            // Temporary hack while sorting out keyvault configuration, maybe move this to Environment variable as not sensitive
            await Task.Delay(750);

            if (!DeviceClients.TryGetValue(registrationID, out deviceClient))
            {
               Log.LogWarning("DevID:{registrationID} AppID:{applicationId} Device provisioning polling TryGet while loop failed", registrationID, applicationId);

               throw new ApplicationException($"DevID:{registrationID} AppID:{applicationId} Device provisioning polling TryGet while loop failed");
            }
         }
         return deviceClient;
      }

      static async Task DeviceTelemetrySend(DeviceClient deviceClient, PayloadV5 payloadObect )
      {
         // Assemble the JSON payload to send to Azure IoT Hub/Central.
         Log.LogInformation("DevID:{registrationID} AppID:{applicationId} Payload assembly start", payloadObect.dev_id, payloadObect.app_id);

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
            Log.LogError(ex, "DevID:{registrationID} AppID:{applicationId} Payload processing or Telemetry event assembly failed", payloadObect.dev_id, payloadObect.app_id);
            throw;
         }

         // Send the message to Azure IoT Hub/Azure IoT Central
         Log.LogInformation("DevID:{registrationID} AppID:{applicationId} Payload SendEventAsync start", payloadObect.dev_id, payloadObect.app_id);
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
               Log.LogWarning("DevID:{registrationID} AppID:{applicationId} Payload SendEventAsync TryRemove failed", payloadObect.dev_id, payloadObect.app_id);
            }

            Log.LogError(ex, "DevID:{registrationID} AppID:{applicationId} SendEventAsync failed", payloadObect.dev_id, payloadObect.app_id);
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
                     throw new ApplicationException($"DevID:{registrationId} AppID:{applicationId} RegisterAsync failed");
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
