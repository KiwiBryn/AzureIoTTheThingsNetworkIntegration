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
namespace devMobile.TheThingsNetwork.HttpIntegrationUplinkHttp.Models
{
   using System;
   using System.Collections.Generic;

   // First version of classes for unpacking HTTP payload https://json2csharp.com/
   public class GatewayV1
   {
      public string gtw_id { get; set; }
      public int timestamp { get; set; }
      public DateTime time { get; set; }
      public int channel { get; set; }
      public int rssi { get; set; }
      public double snr { get; set; }
      public int rf_chain { get; set; }
      public double latitude { get; set; }
      public double longitude { get; set; }
      public int altitude { get; set; }
   }

   public class MetadataV1
   {
      public string time { get; set; }
      public double frequency { get; set; }
      public string modulation { get; set; }
      public string data_rate { get; set; }
      public string coding_rate { get; set; }
      public List<GatewayV1> gateways { get; set; }
   }

   public class PayloadV1
   {
      public string app_id { get; set; }
      public string dev_id { get; set; }
      public string hardware_serial { get; set; }
      public int port { get; set; }
      public int counter { get; set; }
      public bool confirmed { get; set; }
      public string payload_raw { get; set; }
      public MetadataV1 metadata { get; set; }
      public string downlink_url { get; set; }
   }
}
