﻿//---------------------------------------------------------------------------------
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
   using Microsoft.AspNetCore.Mvc;

   using log4net;

   using devMobile.AspNet.ErrorHandling;
   using devMobile.TheThingsNetwork.HttpIntegrationUplinkHttp.Models;

   [Route("[controller]")]
   [ApiController]
   public class ClassSerialisationV1 : ControllerBase
   {
      private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

      public string Index()
      {
         return "ClassSerialisationV1 move along nothing to see";
      }

      [HttpPost]
      public IActionResult Post([FromBody] PayloadV1 payload)
      {
         // Check that the post data is good
         if (!this.ModelState.IsValid)
         {
            log.WarnFormat("ClassSerialisationV1 validation failed {0}", this.ModelState.Messages());

            return this.BadRequest(this.ModelState);
         }
         log.Info($"DevEUI:{payload.hardware_serial} Payload Base64:{payload.payload_raw}");

         return this.Ok();
      }
   }
}
