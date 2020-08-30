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
namespace devMobile.TheThingsNetwork.CustomConvertors
{
   using System;

   using System.Text.Json.Serialization;
   using System.Text.Json;

   public class LongConverter : JsonConverter<long>
   {
      public override long Read(
          ref Utf8JsonReader reader,
          Type typeToConvert,
          JsonSerializerOptions options) =>
              reader.GetInt64();
      public override void Write(
          Utf8JsonWriter writer,
          long longValue,
          JsonSerializerOptions options) =>
              writer.WriteStringValue(longValue.ToString());
   }
}
