using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Common;

namespace IoTHubFunctions
{
    public static class FormatEmail
    {
        private static string _lineSep = "<br />";
        private static string _spaceChar = "&nbsp;";

        private static string NonBreakingSpace(int quantity)
        {
            string text = "";
            for (int i = 0; i < quantity; i++)
                text += _spaceChar;

            return text;
        }

        [FunctionName("FormatEmail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                CameraAnalysisResult cameraAnalysisResult = JsonConvert.DeserializeObject<CameraAnalysisResult>(requestBody);

                List<string> lines = new List<string>() { };

                lines.Add($"Camera: {cameraAnalysisResult.CameraId}");
                foreach (var image in cameraAnalysisResult.ImageAnalysisResults)
                {
                    lines.Add($"{NonBreakingSpace(5)}- Time: {image.Timestamp}");
                    lines.Add($"{NonBreakingSpace(5)}- Image: {image.ImageUri}");
                    lines.Add($"{NonBreakingSpace(5)}- Detected events:");

                    foreach (var result in image.Results)
                    {
                        lines.Add($"{NonBreakingSpace(10)}- {result.TagName}");
                    }
                }

                string text = string.Join(_lineSep, lines);

                return new OkObjectResult(text);
            }
            catch (Exception e)
            {
                log.LogInformation($"FormatEmail failed. Exception message: {e}");
                throw e;
            }
        }
    }
}
