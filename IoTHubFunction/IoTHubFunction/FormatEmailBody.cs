using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;

namespace IoTHubFunction
{
    public static class FormatEmailBody
    {
        [FunctionName("FormatEmailBody")]
        public static async Task<string> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req,
            TraceWriter log)
        {
            try
            {
                log.Info("C# HTTP trigger function processed a request.");

                // Get email body
                string requestBody = await req.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                log.Info($"camera id: {data.CameraId}");

                List<string> bodyLines = new List<string>() { };
                bodyLines.Add($"The following violations were detected from the camera with Id {data.CameraId}");

                // Update container with 'out' for marked image
                string connectionStringPattern = "DefaultEndpointsProtocol=(.*);AccountName=(.*);AccountKey=.*;EndpointSuffix=(.*)";
                Match match = Regex.Match(Environment.GetEnvironmentVariable("ConnectionString"), connectionStringPattern);
                string storageAccountUri = $"{match.Groups[1].Value}://{match.Groups[2].Value}.blob.{match.Groups[3].Value}";

                string blobUriPattern = $"{storageAccountUri}/([0-9a-z]*)/(.*).jpg";
                match = Regex.Match(data.ImageUri, blobUriPattern);
                string containerName = match.Groups[1].Value;

                data.ImageUri = data.ImageUri.Replace(containerName, "out");

                bodyLines.Add($"Image link: {data.ImageUri}");
                foreach (var violation in data.Violations)
                    bodyLines.Add($"  {violation.TagName}");

                string body = string.Join("\n", bodyLines);

                return body;
            }
            catch (Exception e)
            {
                log.Info($"Function failed. Exception message: {e.Message}");
                throw e;
            }
        }
    }
}
