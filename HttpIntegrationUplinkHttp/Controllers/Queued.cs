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
namespace devMobile.TheThingsNetwork.HttpIntegrationUplink.Controllers
{
   using System;
   using System.Text.Json;
   using System.Threading.Tasks;

   using Azure.Storage.Queues;
   using Microsoft.AspNetCore.Mvc;
   using Microsoft.Extensions.Configuration;

   using log4net;

   using devMobile.AspNet.ErrorHandling;
   using devMobile.TheThingsNetwork.HttpIntegrationUplink.Models;


   [Route("[controller]")]
   [ApiController]
   public class Queued : ControllerBase
   {
      private readonly string storageConnectionString;
      private readonly string queueName;
      private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

      public Queued( IConfiguration configuration)
      {
         this.storageConnectionString = configuration.GetSection("AzureStorageConnectionString").Value;
         this.queueName = configuration.GetSection("UplinkQueueName").Value;
      }

      public string Index()
      {
         return "Queued move along nothing to see";
      }

      [HttpPost]
      public async Task<IActionResult> Post([FromBody] PayloadV5 payload)
      {
         string payloadFieldsUnpacked = string.Empty;

         // Check that the post data is good
         if (!this.ModelState.IsValid)
         {
            log.WarnFormat("QueuedController validation failed {0}", this.ModelState.Messages());

            return this.BadRequest(this.ModelState);
         }

         try
         {
            QueueClient queueClient = new QueueClient(storageConnectionString, queueName);

            await queueClient.CreateIfNotExistsAsync();

            await queueClient.SendMessageAsync(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload)));
         }
         catch( Exception ex)
         {
            log.Error("Unable to open/create queue or send message", ex);

            return this.Problem("Unable to open queue (creating if it doesn't exist) or send message", statusCode:500, title:"Uplink payload not sent" );
         }

         return this.Ok();
      }
   }
}