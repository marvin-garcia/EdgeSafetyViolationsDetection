using Newtonsoft.Json;

namespace Common
{
    public class RecognitionResults
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("iteration")]
        public string Iteration { get; set; }
        [JsonProperty("project")]
        public string Project { get; set; }
        [JsonProperty("predictions")]
        public Prediction[] Predictions { get; set; }

        public class Prediction
        {
            [JsonProperty("boundingBox")]
            public BoundingBox BoundingBox { get; set; }
            [JsonProperty("probability")]
            public double Probability { get; set; }
            [JsonProperty("tagId")]
            public string TagId { get; set; }
            [JsonProperty("tagName")]
            public string TagName { get; set; }
        }

        public class BoundingBox
        {
            [JsonProperty("height")]
            public double Height { get; set; }
            [JsonProperty("left")]
            public double Left { get; set; }
            [JsonProperty("top")]
            public double Top { get; set; }
            [JsonProperty("width")]
            public double Width { get; set; }
        }
    }
}