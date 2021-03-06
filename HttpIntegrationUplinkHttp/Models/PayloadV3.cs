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

   // Third version of classes for unpacking HTTP payload https://json2csharp.com/
   public class Gps1V3
   {
      public int altitude { get; set; }
      public double latitude { get; set; }
      public double longitude { get; set; }
   }

   public class PayloadFieldsV3
   {
      public double analog_in_1 { get; set; }
      public int digital_in_1 { get; set; }
      public Gps1V3 gps_1 { get; set; }
      public int luminosity_1 { get; set; }
      public double temperature_1 { get; set; }
   }

   public class GatewayV3 // https://github.com/TheThingsNetwork/ttn/blob/36761935d1867ce2cd70a80ceef197a124e2d276/core/types/gateway_metadata.go
   {
      public string gtw_id { get; set; }
      public ulong timestamp { get; set; }
      public DateTime time { get; set; }
      public int channel { get; set; }
      public int rssi { get; set; }
      public double snr { get; set; }
      public int rf_chain { get; set; }
      public double latitude { get; set; }
      public double longitude { get; set; }
      public int altitude { get; set; }
   }

   public class MetadataV3
   {
      public string time { get; set; }
      public double frequency { get; set; }
      public string modulation { get; set; }
      public string data_rate { get; set; }
      public string coding_rate { get; set; }
      public List<GatewayV3> gateways { get; set; }
   }

   public class PayloadV3
   {
      public string app_id { get; set; }
      public string dev_id { get; set; }
      public string hardware_serial { get; set; }
      public int port { get; set; }
      public int counter { get; set; }
      public bool is_retry { get; set; }
      public string payload_raw { get; set; }
      public PayloadFieldsV3 payload_fields { get; set; }
      public MetadataV3 metadata { get; set; }
      public string downlink_url { get; set; }
   }
}
