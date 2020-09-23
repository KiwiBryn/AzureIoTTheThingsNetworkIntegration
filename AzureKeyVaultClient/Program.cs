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
namespace devMobile.TheThingsNetwork.AzureKeyVaultClient
{
   using System;
   using System.Threading.Tasks;
   using Azure.Core;
   using Azure.Identity;
   using Azure.Security.KeyVault.Secrets;

   class Program
   {
      static async Task Main(string[] args)
      {
         if (args.Length != 2)
         {
            Console.WriteLine("[KeyVaultURI] [SecretName] ");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
            return;
         }

         SecretClientOptions options = new SecretClientOptions()
         {
            Retry =
            {
               Delay= TimeSpan.FromSeconds(2),
               MaxDelay = TimeSpan.FromSeconds(16),
               MaxRetries = 5,
               Mode = RetryMode.Exponential
            }
         };

         try
         {

            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);

            var client = new SecretClient(new Uri(args[0]), credential, options);

            //Retrieve Secret from KeyVault
            KeyVaultSecret secret = await client.GetSecretAsync(args[1]);

            Console.WriteLine($"ID:{secret.Id} value:{ secret.Value}");
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
