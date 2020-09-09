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
namespace devMobile.TheThingsNetwork.HttpIntegrationUplink.Controllers
{
   using System.Text.Json;
   using Microsoft.AspNetCore.Mvc;

   using log4net;

   using devMobile.AspNet.ErrorHandling;
   using devMobile.TheThingsNetwork.HttpIntegrationUplink.Models;

   [Route("[controller]")]
   [ApiController]
   public class ClassSerialisationV4Fields : ControllerBase
   {
      private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

      public string Index()
      {
         return "ClassSerialisationV4Fields move along nothing to see";
      }

      [HttpPost]
      public IActionResult Post([FromBody] PayloadV4 payload)
      {
         string payloadFieldsUnpacked = string.Empty;
         
         // Check that the post data is good
         if (!this.ModelState.IsValid)
         {
            log.WarnFormat("ClassSerialisationV4Fields validation failed {0}", this.ModelState.Messages());

            return this.BadRequest(this.ModelState);
         }

         JsonElement jsonElement = (JsonElement)payload.payload_fields;
         foreach (var property in jsonElement.EnumerateObject())
         {
            // Special handling for nested properties
            if (property.Name.StartsWith("gps_") || property.Name.StartsWith("accelerometer_") || property.Name.StartsWith("gyrometer_"))
            {
               payloadFieldsUnpacked += $"Property Name:{property.Name}\r\n";
               JsonElement gpsElement = (JsonElement)property.Value;
               foreach (var gpsProperty in gpsElement.EnumerateObject())
               {
                  payloadFieldsUnpacked += $" Property Name:{gpsProperty.Name} Property Value:{gpsProperty.Value}\r\n";
               }
            }
            else
            {
               payloadFieldsUnpacked += $"Property Name:{property.Name} Property Value:{property.Value}\r\n";
            }
         }

         log.Info(payloadFieldsUnpacked);

         return this.Ok();
      }
   }
}
