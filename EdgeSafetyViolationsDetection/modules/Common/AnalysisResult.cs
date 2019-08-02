using System;
using System.Linq;
using System.Collections.Generic;

namespace Common
{
    public class AnalysisResult
    {
        public string CameraId { get; set; }
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

    public class AnalysisResultHubMessage
    {

    }

    public class AnalysisResultMetrics
    {
        
    }
}