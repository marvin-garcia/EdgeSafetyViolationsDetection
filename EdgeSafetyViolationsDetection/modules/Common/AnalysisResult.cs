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

            /// Convert predictions to type Result
            public static Result[] Results(IEnumerable<RecognitionResults.Prediction> predictions)
            {
                return predictions
                    .GroupBy(x => x.TagName)
                    .Select(x => x.First())
                    .Select(x => new Result()
                    {
                        TagName = x.TagName,
                        Probability = x.Probability,
                    }).ToArray();
            }
        }
    }

    public class CameraAnalysisResult
    {
        public string FactoryId { get; set; }
        public string CameraId { get; set; }
        public ImageAnalysisResult[] ImageAnalysisResults { get; set; }
    }

    public class FlatImageAnalysisResult
    {
        public string FactoryId { get; set; }
        public string CameraId { get; set; }
        public string ImageUri { get; set; }
        public DateTime Timestamp { get; set; }
        public string TagName { get; set; }
        public double Probability { get; set; }

        public static FlatImageAnalysisResult[] Convert(CameraAnalysisResult cameraAnalysisResult)
        {
            List<FlatImageAnalysisResult> flatImageAnalysisResults = new List<FlatImageAnalysisResult>() { };

            foreach (var image in cameraAnalysisResult.ImageAnalysisResults)
                foreach (var result in image.Results)
                    flatImageAnalysisResults.Add(new FlatImageAnalysisResult()
                    {
                        FactoryId = cameraAnalysisResult.FactoryId,
                        CameraId = cameraAnalysisResult.CameraId,
                        ImageUri = image.ImageUri,
                        Timestamp = image.Timestamp,
                        TagName = result.TagName,
                        Probability = result.Probability,
                    });
            
            return flatImageAnalysisResults.ToArray();
        }
    }
}