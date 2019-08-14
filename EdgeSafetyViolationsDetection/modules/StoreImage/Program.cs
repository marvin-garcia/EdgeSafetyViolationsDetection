namespace StoreImage
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
                StoreImage().Wait();
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
        static async Task StoreImage()
        {
            _counterValue = Interlocked.Increment(ref _counter);
            
            try
            {
                _consoleLogger.LogDebug($"Received kick to store images. Counter: {_counterValue}");

                Task<MessageResponse>[] tasks = new Task<MessageResponse>[_envSettings.CameraDevices.Length];
                for (int i = 0; i < _envSettings.CameraDevices.Length; i++)
                    tasks[i] = StoreImage(_envSettings.CameraDevices[i]);

                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                _consoleLogger.LogCritical("StoreImage caught an exception: {0}", e);
            }

            _consoleLogger.LogDebug($"Processed event.");
        }

        static async Task<MessageResponse> StoreImage(EnvSettings.CameraDevice camera)
        {
            try
            {
                // Check if it is time to capture image
                if (_counterValue % camera.CaptureTimeInterval != 0)
                    return MessageResponse.Abandoned;
                
                _consoleLogger.LogTrace($"Time to capture image for camera {camera.Id}");

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
                
                // Save the image in respective local folder for each AI module's tag
                DateTime utcDate = DateTime.UtcNow;
                TimeZoneInfo localZone = TimeZoneInfo.FindSystemTimeZoneById(camera.TimeZoneId);
                DateTime localDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, localZone);

                string imageName = $"{localDate.ToString("yyyyMMddTHHmmssfff")}";
                foreach (var module in camera.AIModules)
                    foreach (var tag in module.Tags)
                    {
                        string tagFolder = Path.Combine(camera.LocalFolder, camera.FactoryId, module.Name, tag.Name);
                        if (!Directory.Exists(tagFolder))
                            Directory.CreateDirectory(tagFolder);
                        
                        string imagePath = Path.Combine(tagFolder, Path.ChangeExtension(imageName, "jpg"));
                        File.WriteAllBytes(imagePath, byteArray);

                        _consoleLogger.LogDebug($"Saved image to path {imagePath}");
                    }
            }
            catch (Exception e)
            {
                _consoleLogger.LogCritical("StoreImage caught an exception: {0}", e);
            }

            return MessageResponse.Completed;
        }

    }
}
