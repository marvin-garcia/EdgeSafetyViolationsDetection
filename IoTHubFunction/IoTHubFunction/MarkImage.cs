using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;

namespace IoTHubFunction
{
    public static class MarkImage
    {
        private static HttpClient _httpClient = new HttpClient();

        [FunctionName("MarkImage")]
        public static async Task Run(
            [BlobTrigger("dbeshare1/{path}/flagged/{name}.jpg", Connection = "ConnectionString")]Stream imageStream,
            [Blob("out/{path}/flagged/{name}.jpg", FileAccess.Write, Connection = "ConnectionString")] Stream markedImageStream,
            string path,
            string name,
            TraceWriter log)
        {
            log.Info($"C# Blob trigger function Processed blob\n Path:{path} \n Name:{name} \n Size: {imageStream.Length} Bytes");

            try
            {
                // Get predictions result file
                string storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName");
                string jsonFileUrl = $"https://{storageAccountName}.blob.core.windows.net/dbeshare1/{path}/flagged/{name}.json";
                string recognitionResults = await GetStringAsync(jsonFileUrl);
                RecognitionResults.Prediction[] predictions = JsonConvert.DeserializeObject<RecognitionResults.Prediction[]>(recognitionResults);

                using (var image = new Bitmap(imageStream))
                {
                    // Create rectangles from predictions
                    Rectangle[] rectangles = GetRectangles(image.Width, image.Height, predictions.Select(x => x.BoundingBox));

                    // Create marked image
                    var markedImage = new Bitmap(image);
                    using (var graphics = Graphics.FromImage(markedImage))
                    {
                        using (var pen = new Pen(Color.Red, 2))
                            graphics.DrawRectangles(pen, rectangles.ToArray());

                        // Save marked image
                        log.Info($"Saving marked image");
                        markedImage.Save(markedImageStream, ImageFormat.Jpeg);
                    }
                }
            }
            catch (Exception e)
            {
                log.Info($"MarkImage function failed. Exception emssage: {e.Message}");
                throw e;
            }
        }

        static Rectangle[] GetRectangles(int width, int height, IEnumerable<RecognitionResults.BoundingBox> boundingBoxes)
        {
            try
            {
                Rectangle[] rectangles = boundingBoxes.Select(x => new Rectangle(
                        Convert.ToInt32(width * x.Left),
                        Convert.ToInt32(height * x.Top),
                        Convert.ToInt32(width * x.Width),
                        Convert.ToInt32(height * x.Height))).ToArray();

                return rectangles;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        static async Task<string> GetStringAsync(string requestUri)
        {
            try
            {
                int downloadCount = 3;
                string recognitionResults = null;
                do
                {
                    var response = await _httpClient.GetAsync(requestUri);
                    if (!response.IsSuccessStatusCode)
                    {
                        Thread.Sleep(1000);
                        downloadCount--;
                    }

                    recognitionResults = await response.Content.ReadAsStringAsync();
                }
                while (string.IsNullOrEmpty(recognitionResults) && downloadCount != 0);

                return recognitionResults;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
