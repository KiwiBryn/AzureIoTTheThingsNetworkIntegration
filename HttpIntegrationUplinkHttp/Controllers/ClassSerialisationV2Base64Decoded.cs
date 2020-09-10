//---------------------------------------------------------------------------------
// Copyright (c) August 2020, devMobile Software
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
namespace devMobile.TheThingsNetwork.HttpIntegrationUplinkHttp.Controllers
{
   using System;
   using System.Text;
   using Microsoft.AspNetCore.Mvc;

   using log4net;

   using devMobile.AspNet.ErrorHandling;
   using devMobile.TheThingsNetwork.HttpIntegrationUplinkHttp.Models;

   [Route("[controller]")]
   [ApiController]
   public class ClassSerialisationV2Base64Decoded : ControllerBase
   {
      private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

      public string Index()
      {
         return "move along nothing to see";
      }

      [HttpPost]
      public IActionResult Post([FromBody] PayloadV2 payload)
      {
         // Check that the post data is good
         if (!this.ModelState.IsValid)
         {
            log.WarnFormat("ClassSerialisationV2Base64Decoded validation failed {0}", this.ModelState.Messages());

            return this.BadRequest(this.ModelState);
         }

         log.Info($"DevEUI:{payload.hardware_serial} Port:{payload.port} Payload:{ Encoding.UTF8.GetString(Convert.FromBase64String(payload.payload_raw))}");

         return this.Ok();
      }
   }
}
