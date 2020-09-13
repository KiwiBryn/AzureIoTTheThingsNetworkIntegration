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
   using Microsoft.Azure.Storage.Queue;
   using Microsoft.Azure.WebJobs;
   using Microsoft.Extensions.Logging;

   public static class UplinkMessageProcessor
   {
      const string RunTag = "Processor001";
      static int ConcurrentThreadCount = 0;
      static int MessagesProcessed = 0;

      [FunctionName("UplinkMessageProcessor")]
      public static void Run(
         [QueueTrigger("%UplinkQueueName%", Connection = "AzureStorageConnectionString")] 
         CloudQueueMessage cloudQueueMessage, 
         IBinder binder, ILogger log)
      {
         try
         {
            Interlocked.Increment(ref ConcurrentThreadCount);
            Interlocked.Increment(ref MessagesProcessed);

            log.LogInformation($"{MessagesProcessed} {RunTag} Threads:{ConcurrentThreadCount}");

            CloudQueue outputQueue = binder.Bind<CloudQueue>(new QueueAttribute("%UplinkQueueName%"));

            CloudQueueMessage message = new CloudQueueMessage(cloudQueueMessage.AsString);

            outputQueue.AddMessage(message, initialVisibilityDelay: new TimeSpan(0, 5, 0));
    
            Thread.Sleep(2000);

            Interlocked.Decrement(ref ConcurrentThreadCount);
         }
         catch (Exception ex)
         {
            log.LogError(ex, "Processing of Uplink message failed");

            throw;
         }
      }
   }
}