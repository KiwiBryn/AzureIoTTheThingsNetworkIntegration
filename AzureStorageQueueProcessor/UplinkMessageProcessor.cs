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
namespace devMobile.TheThingsNetwork.AzureStorageQueueProcessor
{
   using System;
   using System.Threading;

   using Microsoft.Azure.WebJobs;

   public static class UplinkMessageProcessor
   {
      static int ConcurrentThreadCount = 0;

      [FunctionName("UplinkMessageProcessor")]
      public static void Run([QueueTrigger("%UplinkQueueName%", Connection = "AzureStorageConnectionString")] string myQueueItem, Microsoft.Azure.WebJobs.ExecutionContext executionContext)
      {
         try
         {
            Interlocked.Increment(ref ConcurrentThreadCount);

            Interlocked.Decrement(ref ConcurrentThreadCount);
         }
         catch (Exception ex)
         {

            throw;
         }
      }
   }
}