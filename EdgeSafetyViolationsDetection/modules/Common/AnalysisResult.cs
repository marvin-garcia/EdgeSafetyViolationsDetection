using System;
using System.Linq;
using System.Collections.Generic;

namespace Common
{
    public class ImageAnalysisResult
    {
        public string ImageUri { get; set; }
        public DateTime Timestamp { get; set; }
        public Result[] Results { get; set; }

        public class Result
        {
            public string TagName { get; set; }
            public double Probability { get; set; }
            public bool Notify { get; set; }

            /// Convert predictions to type Result
            public static Result[] Results(IEnumerable<RecognitionResults.Prediction> predictions, EnvSettings.AIModule aiModule)
            {
                return predictions
                    .GroupBy(x => x.TagName)
                    .Select(x => x.First())
                    .Select(x => new Result()
                    {
                        TagName = x.TagName,
                        Probability = x.Probability,
                        Notify = aiModule.Tags.Where(y => y.Name == x.TagName).First().Notify,
                    }).ToArray();
            }
        }
    }

    public class CameraAnalysisResult
    {
        public string FactoryId { get; set; }
        public string CameraId { get; set; }
        public ImageAnalysisResult[] ImageAnalysisResults { get; set; }

        // This constructor is intended to filter results that are supposed to be notified
        public CameraAnalysisResult(string factoryId, string cameraId, IEnumerable<ImageAnalysisResult> imageAnalysisResults)
        {
            this.FactoryId = factoryId;
            this.CameraId = cameraId;
            this.ImageAnalysisResults = imageAnalysisResults.Where(x => x.Results.Where(y => y.Notify).Count() > 0).ToArray();
        }
    }

    public class FlatImageAnalysisResult
    {
        public string FactoryId { get; set; }
        public string CameraId { get; set; }
        public string ImageUri { get; set; }
        public DateTime Timestamp { get; set; }
        public string TagName { get; set; }
        public double Probability { get; set; }

        // public static FlatImageAnalysisResult[] Convert(CameraAnalysisResult cameraAnalysisResult)
        // {
        //     List<FlatImageAnalysisResult> flatImageAnalysisResults = new List<FlatImageAnalysisResult>() { };

        //     foreach (var image in cameraAnalysisResult.ImageAnalysisResults)
        //         foreach (var result in image.Results)
        //             flatImageAnalysisResults.Add(new FlatImageAnalysisResult()
        //             {
        //                 FactoryId = cameraAnalysisResult.FactoryId,
        //                 CameraId = cameraAnalysisResult.CameraId,
        //                 ImageUri = image.ImageUri,
        //                 Timestamp = image.Timestamp,
        //                 TagName = result.TagName,
        //                 Probability = result.Probability,
        //             });
            
        //     return flatImageAnalysisResults.ToArray();
        // }

        public static FlatImageAnalysisResult[] Convert(string factoryId, string cameraId, ImageAnalysisResult imageAnalysisResult)
        {
            List<FlatImageAnalysisResult> flatImageAnalysisResults = new List<FlatImageAnalysisResult>() { };

            foreach (var result in imageAnalysisResult.Results)
                flatImageAnalysisResults.Add(new FlatImageAnalysisResult()
                {
                    FactoryId = factoryId,
                    CameraId = cameraId,
                    ImageUri = imageAnalysisResult.ImageUri,
                    Timestamp = imageAnalysisResult.Timestamp,
                    TagName = result.TagName,
                    Probability = result.Probability,
                });
            
            return flatImageAnalysisResults.ToArray();
        }
    }
}