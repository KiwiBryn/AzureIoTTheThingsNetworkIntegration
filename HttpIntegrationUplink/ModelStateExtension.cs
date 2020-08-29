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
namespace devMobile.AspNet.ErrorHandling
{
   using System;
   using System.Linq;

   using Microsoft.AspNetCore.Mvc.ModelBinding;

	/// <summary>
	/// Extension class for ModelState error dictionary.
	/// </summary>
   public static class ModelStateExtension
   {
		/// <summary>
		/// Flattens the model state dictionary into a string for logging.
		/// </summary>
		/// <param name="modelState">Dictionary containing information about why model validation failed.</param>
		/// <returns>string containing errors.</returns>
      public static string Messages(this ModelStateDictionary modelState)
      {
         return string.Join(Environment.NewLine, modelState.Values.SelectMany(v => v.Errors).Select(v => v.ErrorMessage + " " + v.Exception));
      }
   }
}