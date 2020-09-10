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
namespace devMobile.TheThingsNetwork.HttpIntegrationUplinkFunction
{
   using System;
   using System.IO;
   using System.Threading.Tasks;
   using Microsoft.AspNetCore.Http;
   using Microsoft.Azure.WebJobs;
   using Microsoft.Azure.WebJobs.Extensions.Http;
   using Microsoft.Extensions.Logging;

   public static class UplinkProcessor
   {
      [FunctionName("UplinkProcessor")]
      [return: Queue("%UplinkQueueName%", Connection = "AzureStorageConnectionString")]
      public static async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest request, ILogger log)
      {
         string payload;

         try
         {
            log.LogInformation($"Processing Uplink HTTP post");

            using (StreamReader streamReader = new StreamReader(request.Body))
            {
               payload = await streamReader.ReadToEndAsync();
            }
         }
         catch( Exception ex)
         {
            log.LogError(ex, "Processing of Uplink HTTP post failed");

            throw;
         }

         return payload;
      }
   }
}
