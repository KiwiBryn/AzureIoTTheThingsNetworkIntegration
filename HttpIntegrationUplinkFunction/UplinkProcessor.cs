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
   using System.IO;
   using Microsoft.AspNetCore.Http;
   using Microsoft.Azure.WebJobs;
   using Microsoft.Extensions.Logging;

   public static class UplinkProcessor
   {
      [FunctionName("UplinkProcessor")]
      [return: Queue("%UplinkQueueName%", Connection = "AzureStorageConnectionString")]
      public static string Run([HttpTrigger("post", Route = null)] HttpRequest request, ILogger log)
      {
         string input;

         log.LogInformation("C# HTTP trigger function processed a request.");

         using (StreamReader streamReader = new StreamReader(request.Body))
         {
            input = new StreamReader(request.Body).ReadToEnd();
         }

         return input;
      }
   }
}
