namespace ImageAnalyzer
{
    using System;
    using System.IO;
    using System.Text;
    using System.Linq;
    using System.Timers;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Runtime.Loader;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using Newtonsoft.Json;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    
    class Program
    {
        static int counter;
        private static HttpClient _httpClient = new HttpClient();
        private static System.Timers.Timer EventTimer;
        private static EnvSettings _envSettings;
        private static ModuleClient _ioTHubModuleClient;
        private static ILoggerFactory _loggerFactory;
        private static ILogger _consoleLogger;
        
        /// <summary>
        /// Main method triggered at startup.
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                // Initialize logger
                var logLevel = (LogLevel)Enum.Parse(typeof(LogLevel), Environment.GetEnvironmentVariable("LOG_LEVEL"), true);
                var consoleLoggerConfiguration = new ConsoleLoggerConfiguration(logLevel: logLevel);
                _loggerFactory = ConsoleLoggerExtensions.AddConsoleLogger(new LoggerFactory(), consoleLoggerConfiguration);
                _consoleLogger = _loggerFactory.CreateLogger<ConsoleLogger>();

                _consoleLogger.LogInformation("Kicking off Main method");
                
                // Get env settings file URL
                string envSettingsString = _httpClient.GetStringAsync(Environment.GetEnvironmentVariable("ENV_SETTINGS_URL")).ContinueWith((r) =>
                {
                    return r.Result;
                }).Result;
                _envSettings = JsonConvert.DeserializeObject<EnvSettings>(envSettingsString);

                _consoleLogger.LogInformation($"Retrieved env settings successfully");

                // Use Timer trigger instead
                SetTimer(_envSettings.TimerDelayInSeconds * 1000);

                // Wait until the app unloads or is cancelled
                var cts = new CancellationTokenSource();
                AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
                Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
                WhenCancelled(cts.Token).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Main caught an exception: {e}");
            }
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            try
            {
                var tcs = new TaskCompletionSource<bool>();
                cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
                return tcs.Task;
            }
            catch (Exception e)
            {
                _consoleLogger.LogInformation($"WhenCancelled caught an exception: {e}");
                throw e;
            }
        }

        /// <summary>
        /// Initializes module client for connectivity with the Edge runtime.
        /// </summary>
        static async Task InitializeModuleClient()
        {
            try
            {
                MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                ITransportSettings[] settings = { mqttSetting };

                // Open a connection to the Edge runtime
                _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                await _ioTHubModuleClient.OpenAsync();
                
                _consoleLogger.LogInformation("IoT Hub module client initialized.");
            }
            catch (Exception e)
            {
                _consoleLogger.LogInformation($"InitializeModuleClient caught an exception: {e}");
            }
        }

        /// <summary>
        /// Sets event timer with a callback for a method.
        /// </summary>
        static void SetTimer(int timerDelay)
        {
            try
            {
                _consoleLogger.LogInformation("Setting timer with {0} delay", timerDelay);

                EventTimer = new System.Timers.Timer(timerDelay);
                EventTimer.Elapsed += (sender, e) => OnTimedEvent(sender, e);
                EventTimer.AutoReset = true;
                EventTimer.Enabled = true;
            }
            catch (Exception e)
            {
                _consoleLogger.LogInformation($"SetTimer caught an exception: {e}");
            }
        }

        /// <summary>
        /// Callback method to be executed every time the timer resets.
        /// </summary>
        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                AnalyzeImage().Wait();
            }
            catch (Exception ex)
            {
                _consoleLogger.LogInformation($"OnTimedEvent caught an exception: {ex.Message}");
            }
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the IoT Edge Hub. 
        /// This method Retrieves an image from the available cameras via GET.
        /// This method also forms the absolute output file path using the camera Id, a time stamp and the OutputFolderPath. It then copies the images to output file.
        /// </summary>
        static async Task AnalyzeImage()
        {
            int counterValue = Interlocked.Increment(ref counter);
            
            try
            {
                _consoleLogger.LogDebug($"Received kick to analyze images. Counter: {counterValue}");

                Task<MessageResponse>[] tasks = new Task<MessageResponse>[_envSettings.CameraDevices.Length];
                for (int i = 0; i < _envSettings.CameraDevices.Length; i++)
                    tasks[i] = AnalyzeImage(_envSettings.CameraDevices[i]);

                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                _consoleLogger.LogCritical("AnalyzeImage caught an exception: {0}", e);
            }

            _consoleLogger.LogDebug($"Processed event.");
        }

        static async Task<MessageResponse> AnalyzeImage(EnvSettings.CameraDevice camera)
        {
            try
            {
                // Get camera credentials
                string plainCredentials = $"{camera.Username}:{camera.Password}";
                string svcCredentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(plainCredentials));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", svcCredentials);
                
                _consoleLogger.LogDebug($"Camera URL: {camera.ImageEndpoint}");

                // Make GET request to get image
                var response = await _httpClient.GetAsync(camera.ImageEndpoint);
                if (!response.IsSuccessStatusCode)
                {
                    _consoleLogger.LogError($"Failed to make GET request to camera {camera.Id}. Response: {response.ReasonPhrase}");
                    return MessageResponse.Abandoned;
                }
                else
                    _consoleLogger.LogDebug($"Get request to camera {camera.Id} was successful");

                byte[] byteArray = await response.Content.ReadAsByteArrayAsync();
                
                using (ByteArrayContent content = new ByteArrayContent(byteArray))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    
                    // Call scoring endpoint(s)
                    foreach (var module in camera.AIModules)
                    {
                        _consoleLogger.LogDebug($"Module URL: {module.ScoringEndpoint}");

                        response = await _httpClient.PostAsync(module.ScoringEndpoint, content);
                        if (!response.IsSuccessStatusCode)
                        {
                            _consoleLogger.LogError($"Failed to make POST request to module {module.ScoringEndpoint}. Response: {response.ReasonPhrase}");
                            return MessageResponse.Abandoned;
                        }
                        else
                            _consoleLogger.LogDebug($"POST request to module {module.ScoringEndpoint} was successful");

                        var contentString = await response.Content.ReadAsStringAsync();
                        var recognitionResults = JsonConvert.DeserializeObject<RecognitionResults>(contentString);

                        var flaggedTags = recognitionResults.Predictions.Where(x => module.Tags.Select(t => t.Name).Contains(x.TagName));
                        var tagsOverThreshold = flaggedTags.Where(x => x.Probability >= module.Tags.Where(y => y.Name == x.TagName).First().Probability);
                        
                        string folderName;
                        string fileName = $"{DateTime.Now.ToString("yyyyMMddTHHmmssfff")}";
                        RecognitionResults.Prediction[] predictions = new RecognitionResults.Prediction[] { };

                        // Save to flagged folder
                        if (tagsOverThreshold.Count() > 0)
                        {
                            predictions = tagsOverThreshold.ToArray();;

                            folderName = "flagged";
                            string imageUri = $"https://{_envSettings.StorageAccountName}.blob.core.windows.net/{_envSettings.DBEShareContainerName}/{folderName}/{Path.ChangeExtension(fileName, "jpg")}";
                            
                            _consoleLogger.LogDebug($"Found some tags: {string.Join(", ", tagsOverThreshold.Select(x => x.TagName))}");

                            // Send message to hub
                            var message = new
                            {
                                CameraId = camera.Id,
                                TimeStamp = DateTime.Now,
                                ImageUri = imageUri,
                                Violations = tagsOverThreshold
                                    .GroupBy(x => x.TagName)
                                    .Select(x => x.First())
                                    .Select(x => new
                                    { 
                                        x.TagName,
                                        x.Probability,
                                    })
                            };

                            var properties = new Dictionary<string, string>()
                            {
                                { "cameraId", camera.Id },
                                { "moduleEndpoint", module.ScoringEndpoint },
                                { "TimeStamp", DateTime.Now.ToString("yyyyMMddTHHmmssfff") },
                                { "ImageUri", imageUri },
                            };
                            
                            await SendMessageToHub(JsonConvert.SerializeObject(message), properties);
                        }
                        // Save to safe folder
                        else
                        {
                            predictions = recognitionResults.Predictions;

                            folderName = "safe";
                            _consoleLogger.LogDebug($"No tags were found");
                        }

                        // Set output directory
                        string outputDirectory = Path.Combine(_envSettings.OutputFolder, camera.Id, folderName);
                        if (!Directory.Exists(outputDirectory))
                            Directory.CreateDirectory(outputDirectory);

                        // Save image
                        string imageOutputPath = Path.Combine(outputDirectory, Path.ChangeExtension(fileName, "jpg"));
                        File.WriteAllBytes(imageOutputPath, byteArray);

                        // Save payload
                        string fileOutputPath = Path.Combine(outputDirectory, Path.ChangeExtension(fileName, "json"));
                        File.WriteAllText(fileOutputPath, JsonConvert.SerializeObject(predictions));
                    }
                }
            }
            catch (Exception e)
            {
                _consoleLogger.LogCritical("AnalyzeImage caught an exception: {0}", e);
            }

            return MessageResponse.Completed;
        }

        static async Task SendMessageToHub(string messageString, Dictionary<string, string> properties)
        {
            try
            {
                if (_ioTHubModuleClient == null)
                {
                    _consoleLogger.LogCritical($"SendMessageToHub: ModuleClient doesn't exist");
                    return;
                }
                
                var pipeMessage = new Message(Encoding.UTF8.GetBytes(messageString));
                foreach (var prop in properties)
                    pipeMessage.Properties.Add(prop.Key, prop.Value);
                
                await _ioTHubModuleClient.SendEventAsync("output1", pipeMessage);

                _consoleLogger.LogTrace("Hub message was sent successfully");

                return;
            }
            catch (Exception e)
            {
                _consoleLogger.LogCritical("SendMessageToHub caught an exception: {0}", e);
            }
        }
    }
}
