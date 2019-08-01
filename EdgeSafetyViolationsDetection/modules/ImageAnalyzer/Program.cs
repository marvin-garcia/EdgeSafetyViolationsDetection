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
    using Common;
    
    class Program
    {
        private static int _counter;
        private static int _counterValue;
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
                // Get env settings file URL
                string envSettingsString = _httpClient.GetStringAsync(Environment.GetEnvironmentVariable("ENV_SETTINGS_URL")).ContinueWith((r) =>
                {
                    return r.Result;
                }).Result;
                _envSettings = JsonConvert.DeserializeObject<EnvSettings>(envSettingsString);

                Console.WriteLine($"Retrieved env settings successfully");

                // Initialize logger
                var logLevelProperty = _envSettings.Properties.Where(x => x.Name == "LogLevel").FirstOrDefault();
                if (logLevelProperty == null)
                    throw new Exception("Unable to find LogLevel property in env settings");

                Console.WriteLine($"Setting log level to {logLevelProperty.Value}");
                var logLevel = (LogLevel)Enum.Parse(typeof(LogLevel), logLevelProperty.Value, true);
                var consoleLoggerConfiguration = new ConsoleLoggerConfiguration(logLevel: logLevel);
                _loggerFactory = ConsoleLoggerExtensions.AddConsoleLogger(new LoggerFactory(), consoleLoggerConfiguration);
                _consoleLogger = _loggerFactory.CreateLogger<ConsoleLogger>();

                _consoleLogger.LogInformation("Kicking off Main method");
                
                // Use Timer trigger instead
                var timerIntervalProperty = _envSettings.Properties.Where(x => x.Name == "TimerIntervalInSeconds").FirstOrDefault();
                if (timerIntervalProperty == null)
                    throw new Exception("Unable to find TimerIntervalInSeconds in env settings");
                
                SetTimer(Convert.ToInt32(timerIntervalProperty.Value) * 1000);

                // Initialize module client
                InitializeModuleClient().Wait();

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
            _counterValue = Interlocked.Increment(ref _counter);
            
            try
            {
                _consoleLogger.LogDebug($"Received kick to analyze images. Counter: {_counterValue}");

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
                // Get details for AI module tags' folders
                foreach (var module in camera.AIModules)
                {
                    foreach (var tag in module.Tags)
                    {
                        // Check if it is time to analyze images for current tag
                        if (_counterValue % tag.AnalyzeTimeInterval != 0)
                            continue;
                        
                        // Analyze each image in the tag's folder
                        string tagFolder = Path.Combine(camera.LocalFolder, module.Name, tag.Name);
                        string[] images = Directory.GetFiles(tagFolder);

                        _consoleLogger.LogDebug($"Found {images.Length} images in folder {tagFolder}");

                        // Analyze image files asyncrhonously
                        Task[] tasks = new Task[images.Length];
                        for (int i = 0; i < images.Length; i++)
                        {
                            string filePath = Path.Combine(tagFolder, images[i]);
                            tasks[i] = AnalyzeImage(filePath, camera.Id, module, tag, camera.OutputFolder);
                        }

                        // Wait for tasks to complete
                        await Task.WhenAll(tasks);

                        /// Clean up local tag folder is not needed since
                        /// every image is deleted during the inner method.
                    }
                }
            }
            catch (Exception e)
            {
                _consoleLogger.LogCritical("AnalyzeImage caught an exception: {0}", e);
            }

            return MessageResponse.Completed;
        }

        static async Task AnalyzeImage(string filePath, string cameraId, EnvSettings.AIModule module, EnvSettings.AIModule.Tag tag, string outputFolder)
        {
            try
            {
                // Get output directory details
                string storageAccountName = _envSettings.GetProperty("StorageAccountName");
                string  dbeShareContainerName = _envSettings.GetProperty("DBEShareContainerName");
                string flaggedFolder = "flagged";
                string nonFlaggedFolder = "safe";

                // Read image
                byte[] byteArray = File.ReadAllBytes(filePath);
                using (ByteArrayContent content = new ByteArrayContent(byteArray))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    
                    var response = await _httpClient.PostAsync(module.ScoringEndpoint, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        _consoleLogger.LogError($"Failed to make POST request to module {module.ScoringEndpoint}. Response: {response.ReasonPhrase}");
                        return;
                    }
                    else
                        _consoleLogger.LogDebug($"POST request to module {module.ScoringEndpoint} was successful");

                    var contentString = await response.Content.ReadAsStringAsync();
                    var recognitionResults = JsonConvert.DeserializeObject<RecognitionResults>(contentString);

                    /// Need to differentiate between the current tag being flagged and 
                    /// any other tags from this module in order to mark the image appropriately.
                    /// Logic invites to think that in case the current tag is flagged, 
                    /// it will also be in the all flagged tags list.
                    var currentTagFlagged = recognitionResults.Predictions.Where(x => x.TagName == tag.Name && x.Probability >= tag.Probability);
                    var allFlaggedTags = recognitionResults.Predictions.Where(x => module.Tags.Where(y => x.TagName == y.Name && x.Probability >= y.Probability).Count() > 0);
                    
                    //var flaggedTags = recognitionResults.Predictions.Where(x => x.TagName == tag.Name && x.Probability >= tag.Probability);
                    //var tagsOverThreshold = flaggedTags.Where(x => x.Probability >= module.Tags.Where(y => y.Name == x.TagName).First().Probability);
                    
                    // Send message if current tag is flagged
                    string fileName = Path.GetFileName(filePath);
                    if (currentTagFlagged.Count() > 0)
                    {
                        string imageUri = $"https://{storageAccountName}.blob.core.windows.net/{dbeShareContainerName}/{cameraId}/{flaggedFolder}/{fileName}";
                        
                        _consoleLogger.LogDebug($"Found some tags for image {filePath}: {string.Join(", ", currentTagFlagged.Select(x => x.TagName))}");

                        var message = new
                        {
                            CameraId = cameraId,
                            TimeStamp = DateTime.Now,
                            ImageUri = imageUri,
                            Violations = currentTagFlagged
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
                            { "cameraId", cameraId },
                            { "moduleEndpoint", module.ScoringEndpoint },
                            { "TimeStamp", DateTime.Now.ToString("yyyyMMddTHHmmssfff") },
                            { "ImageUri", imageUri },
                        };
                        
                        await SendMessageToHub(JsonConvert.SerializeObject(message), properties);
                    }
                    else
                        _consoleLogger.LogDebug($"No tags were found in image {filePath}");

                    // Save image to output directory
                    string destinationFolder = allFlaggedTags.Count() > 0 ? flaggedFolder : nonFlaggedFolder;
                    
                    // Set output directory
                    string outputDirectory = Path.Combine(outputFolder, destinationFolder);
                    if (!Directory.Exists(outputDirectory))
                        Directory.CreateDirectory(outputDirectory);

                    // Save image
                    string imageOutputPath = Path.Combine(outputDirectory, fileName);
                    File.WriteAllBytes(imageOutputPath, byteArray);
                    _consoleLogger.LogDebug($"Moving image to final destination folder {imageOutputPath}");

                    // Save payload
                    string fileOutputPath = Path.Combine(outputDirectory, Path.ChangeExtension(fileName, "json"));
                    File.WriteAllText(fileOutputPath, JsonConvert.SerializeObject(recognitionResults.Predictions));

                    // Delete image from local folder
                    File.Delete(filePath);
                }
            }
            catch (Exception e)
            {
                _consoleLogger.LogCritical("AnalyzeImage caught an exception: {0}", e);
            }
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
