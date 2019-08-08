using System.Linq;

namespace Common
{
    public class EnvSettings
    {
        public class CameraDevice
        {
            public string Id { get; set; }
            public string FactoryId { get; set; }
            public string ImageEndpoint { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string LocalFolder { get; set; }
            public string OutputFolder { get; set; }
            public int CaptureTimeInterval { get; set; }
            public AIModule[] AIModules { get; set; }
            public string TimeZoneId { get; set; }
        }

        public class AIModule
        {
            public string Name { get; set; }
            public string ScoringEndpoint { get; set; }
            public Tag[] Tags { get; set; }

            public class Tag
            {
                public string Name { get; set; }
                public double Probability { get; set; }
                public int AnalyzeTimeInterval { get; set; }
            }
        }

        public class Property
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public CameraDevice[] CameraDevices { get; set; }
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