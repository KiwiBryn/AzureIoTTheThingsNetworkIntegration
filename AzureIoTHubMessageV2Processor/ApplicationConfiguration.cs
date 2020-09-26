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
   using Microsoft.Extensions.Configuration;

   public class ApplicationConfiguration
   {
      const string DpsGlobaDeviceEndpointDefault = "global.azure-devices-provisioning.net";

      private IConfiguration Configuration;

      public void Initialise( )
      {
         // Check that KeyVault URI is configured in environment variables. Not a lot we can do if it isn't....
         if (Configuration == null)
         {
            string keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultURI");
            if (string.IsNullOrEmpty(keyVaultUri))
            {
               throw new ApplicationException("KeyVaultURI environment variable not set");
            }

            // Load configuration from KeyVault 
            Configuration = new ConfigurationBuilder()
               .AddEnvironmentVariables()
               .AddAzureKeyVault(keyVaultUri)
               .Build();
         }
      }

      public string DpsGlobaDeviceEndpointResolve()
      {
         string globaDeviceEndpoint = Configuration.GetSection("DPSGlobaDeviceEndpoint").Value;
         if (string.IsNullOrEmpty(globaDeviceEndpoint))
         {
            globaDeviceEndpoint = DpsGlobaDeviceEndpointDefault;
         }

         if (string.IsNullOrWhiteSpace(globaDeviceEndpoint))
         {
            throw new ApplicationException($"DPSGlobaDeviceEndpoint configuration invalid");
         }

         return globaDeviceEndpoint;
      }

      public string DpsIdScopeResolve(string applicationId, int port)
      {
         // Check to see if there is application specific configuration, otherwise run with default
         string idScope = Configuration.GetSection($"DPSIDScope-{applicationId}").Value;
         if (idScope == null)
         {
            idScope = Configuration.GetSection("DPSIDScopeDefault").Value;
         }

         if (string.IsNullOrWhiteSpace(idScope))
         {
            throw new ApplicationException($"DPSIDScope configuration invalid");
         }

         return idScope;
      }

      public string DpsEnrollmentGroupSymmetricKeyResolve(string applicationId, int port)
      {
         // Check to see if there is application specific configuration, otherwise run with default
         string enrollmentGroupSymmetricKey = Configuration.GetSection($"DPSEnrollmentGroupSymmetricKey-{applicationId}").Value;
         if (enrollmentGroupSymmetricKey == null)
         {
            enrollmentGroupSymmetricKey = Configuration.GetSection("DPSEnrollmentGroupSymmetricKeyDefault").Value;
         }

         if (string.IsNullOrWhiteSpace(enrollmentGroupSymmetricKey))
         {
            throw new ApplicationException($"DPSEnrollmentGroupSymmetricKey configuration invalid");
         }

         return enrollmentGroupSymmetricKey;
      }

      public int DpsDeviceProvisioningPollingDelay()
      {
         int deviceProvisioningPollingDelay;

         if (!int.TryParse(Configuration.GetSection("DeviceProvisioningPollingDelay").Value, out deviceProvisioningPollingDelay))
         {
            throw new ApplicationException($"DeviceProvisioningPollingDelay configuration invalid");
         }

         return deviceProvisioningPollingDelay;
      }

      public  string RegistrationIdResolve(string applicationId, int port, string deviceId)
      {
         return deviceId;
      }
   }
}
