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
namespace devMobile.TheThingsNetwork.AzureDeviceProvisioningServiceClient
{
   using System;
   using System.Security.Cryptography;
   using System.Text;
   using System.Threading.Tasks;

   using Microsoft.Azure.Devices.Client;
   using Microsoft.Azure.Devices.Provisioning.Client;
   using Microsoft.Azure.Devices.Provisioning.Client.Transport;
   using Microsoft.Azure.Devices.Shared;

   class Program
   {
      private const string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";

      static async Task Main(string[] args)
      {
         string registrationId;

         if ( args.Length != 4 )
         {
            Console.WriteLine("E registrationID scopeID DeviceSymmetricKey");
            Console.WriteLine("  or");
            Console.WriteLine("E registrationID scopeID GroupSymmetricKey");
            Console.WriteLine("K registrationID PrimaryGroupKey SecondaryGroupKey");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
         }
         registrationId = args[1];

         switch (args[0])
         {
            case "e":
            case "E":
               string scopeId = args[2];
               string symmetricKey = args[3];

               Console.WriteLine($"Enrolllment RegistrationID:{ registrationId} ScopeID:{scopeId}");
               await Enrollement(registrationId, scopeId, symmetricKey);
               break;
            case "k":
            case "K":
               string primaryKey = args[2];
               string secondaryKey = args[3];

               Console.WriteLine($"Enrollment Keys RegistrationID:{ registrationId}");
               GroupEnrollementKeys(registrationId, primaryKey, secondaryKey);
               break;
            case "":
               break;
            default:
               Console.WriteLine("Unknown option");
               break;
         }

         Console.WriteLine("Press <enter> to exit");
         Console.ReadLine();
      }

      static async Task Enrollement(string registrationId, string scopeId, string symetricKey)
      {
         try
         {
            using (var securityProvider = new SecurityProviderSymmetricKey(registrationId, symetricKey, null))
            {
               using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
               {
                  ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(GlobalDeviceEndpoint, scopeId, securityProvider, transport);

                  DeviceRegistrationResult result = await provClient.RegisterAsync();

                  Console.WriteLine($"Hub:{result.AssignedHub} DeviceID:{result.DeviceId} RegistrationID:{result.RegistrationId} Status:{result.Status}");
                  if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                  {
                     Console.WriteLine($"DeviceID{ result.Status} already assigned");
                  }

                  IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                  using (DeviceClient iotClient = DeviceClient.Create(result.AssignedHub, authentication, TransportType.Amqp))
                  {
                     Console.WriteLine("DeviceClient OpenAsync.");
                     await iotClient.OpenAsync().ConfigureAwait(false);
                     Console.WriteLine("DeviceClient SendEventAsync.");
                     await iotClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes("TestMessage"))).ConfigureAwait(false);
                     Console.WriteLine("DeviceClient CloseAsync.");
                     await iotClient.CloseAsync().ConfigureAwait(false);
                  }
               }
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine(ex.Message);
         }
      }

      static void GroupEnrollementKeys(string registrationId, string primaryKey, string secondaryKey)
      {
         string primaryDeviceKey = ComputeDerivedSymmetricKey(Convert.FromBase64String(primaryKey), registrationId);
         string secondaryDeviceKey = ComputeDerivedSymmetricKey(Convert.FromBase64String(secondaryKey), registrationId);

         Console.WriteLine($"RegistrationID:{registrationId}");
         Console.WriteLine($" PrimaryDeviceKey:{primaryDeviceKey}");
         Console.WriteLine($" SecondaryDeviceKey:{secondaryDeviceKey}");
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
