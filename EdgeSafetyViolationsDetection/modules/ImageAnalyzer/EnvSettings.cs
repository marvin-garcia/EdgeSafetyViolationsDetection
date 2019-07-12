namespace ImageAnalyzer
{
    public class EnvSettings
    {
        public class CameraDevice
        {
            public string Id { get; set; }
            public string ImageEndpoint { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public AIModule[] AIModules { get; set; }
        }

        public class AIModule
        {
            public string ScoringEndpoint { get; set; }
            public Tag[] Tags { get; set; }

            public class Tag
            {
                public string Name { get; set; }
                public double Probability { get; set; }
            }
        }

        public CameraDevice[] CameraDevices { get; set; }
        public string LocalFolder { get; set; }
        public string OutputFolder { get; set; }
        public string StorageAccountName { get; set; }
        public string DBEShareContainerName { get; set; }
        public int TimerDelayInSeconds { get; set; }
    }
}