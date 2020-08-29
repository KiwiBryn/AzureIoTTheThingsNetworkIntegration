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
namespace devMobile.TheThingNetwork.HttpIntegrationUplink.Controllers
{
   using System.Text.Json;
   using Microsoft.AspNetCore.Mvc;

   using log4net;

   [Route("[controller]")]
   [ApiController]
   public class Raw : ControllerBase
   {
      private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

      [HttpGet]
      public string Index()
      {
         return "move along nothing to see";
      }

      [HttpPost]
      public void PostRaw([FromBody]JsonElement body)
      {
         string json = JsonSerializer.Serialize(body);

         log.Info(json);
      }
   }
}
