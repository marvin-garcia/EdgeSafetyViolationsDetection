using System.Linq;
using Newtonsoft.Json;

namespace Common
{
    public class EnvSettings
    {
        public class CameraDevice
        {
            [JsonProperty("id")]
            private string id { get; set; }
            public string Id { get { return id.ToLower(); } set { id = value; } }
            [JsonProperty("factoryId")]
            private string factoryId { get; set; }
            public string FactoryId { get { return factoryId.ToLower(); } set { factoryId = value; } }
            [JsonProperty("imageEndpoint")]
            public string ImageEndpoint { get; set; }
            [JsonProperty("username")]
            public string Username { get; set; }
            [JsonProperty("password")]
            public string Password { get; set; }
            [JsonProperty("localFolder")]
            public string LocalFolder { get; set; }
            [JsonProperty("outputFolder")]
            public string OutputFolder { get; set; }
            [JsonProperty("captureTimeInterval")]
            public int CaptureTimeInterval { get; set; }
            [JsonProperty("aiModules")]
            public AIModule[] AIModules { get; set; }
            [JsonProperty("timeZoneId")]
            public string TimeZoneId { get; set; }
        }

        public class AIModule
        {
            [JsonProperty("name")]
            private string name { get; set; }
            public string Name { get { return name.ToLower(); } set { name = value; } }
            [JsonProperty("scoringEndpoint")]
            public string ScoringEndpoint { get; set; }
            [JsonProperty("tags")]
            public Tag[] Tags { get; set; }

            public class Tag
            {
                [JsonProperty("name")]
                private string name { get; set; }
                public string Name { get { return name.ToLower(); } set { name = value; } }
                [JsonProperty("probability")]
                public double Probability { get; set; }
                [JsonProperty("analyzeTimeInterval")]
                public int AnalyzeTimeInterval { get; set; }
            }
        }

        public class Property
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("value")]
            public string Value { get; set; }
        }

        [JsonProperty("cameraDevices")]
        public CameraDevice[] CameraDevices { get; set; }
        [JsonProperty("properties")]
        public Property[] Properties { get; set; }

        public string GetProperty(string name)
        {
            var property = this.Properties.Where(x => x.Name == name).FirstOrDefault();
            if (property == null)
                return null;
            else
                return property.Value;
        }
    }
}