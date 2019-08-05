using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Common;

namespace IoTHubFunctions
{
    public static class MarkBoundingBoxes
    {
        private static HttpClient _httpClient = new HttpClient();

        //[FunctionName("MarkBoundingBoxes")]
        //public static void Run([BlobTrigger("samples-workitems/{name}", Connection = "storageconnection")]Stream myBlob, string name, ILogger log)
        //{
        //    log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

        //    try
        //    {
        //        // Get predictions result file
        //        string storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName");
        //        string jsonFileUrl = $"https://{storageAccountName}.blob.core.windows.net/dbeshare1/{path}/flagged/{name}.json";
        //        string recognitionResults = await GetStringAsync(jsonFileUrl);
        //        RecognitionResults.Prediction[] predictions = JsonConvert.DeserializeObject<RecognitionResults.Prediction[]>(recognitionResults);

        //        using (var image = new Bitmap(imageStream))
        //        {
        //            // Create rectangles from predictions
        //            Rectangle[] rectangles = GetRectangles(image.Width, image.Height, predictions.Select(x => x.BoundingBox));

        //            // Create marked image
        //            var markedImage = new Bitmap(image);
        //            using (var graphics = Graphics.FromImage(markedImage))
        //            {
        //                using (var pen = new Pen(Color.Red, 2))
        //                    graphics.DrawRectangles(pen, rectangles.ToArray());

        //                // Save marked image
        //                log.Info($"Saving marked image");
        //                markedImage.Save(markedImageStream, ImageFormat.Jpeg);
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        log.Info($"MarkImage function failed. Exception emssage: {e.Message}");
        //        throw e;
        //    }
            //}

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
