using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using NetCoreAudio;
// led and ptt
using ConversationalSpeaker.PlatformAbstractions;


namespace ConversationalSpeaker
{
    /// <summary>
    /// A hosted service providing the primary conversation loop for Semantic Kernel with OpenAI ChatGPT.
    /// </summary>
    internal class HostedService : IHostedService, IDisposable
    {
        private readonly ILogger<HostedService> _logger;

        // Semantic Kernel chat support
        private readonly IKernel _semanticKernel;
        private readonly IDictionary<string, ISKFunction> _speechSkill;
        private readonly AzCognitiveServicesWakeWordListener _wakeWordListener;
        private readonly IChatCompletion _chatCompletion;
        private readonly OpenAIChatHistory _chatHistory;
        private readonly ChatRequestSettings _chatRequestSettings;
        //led and ptt
        private IStatusLed _statusLed;
        private IStartTrigger _startTrigger;


        private bool _isTriggered = false;


        
        // random greeting
        private readonly List<string> _greetings = new List<string>
        {
            "Hey!",
            "Hallo!",
            "Hi!",
            "Was gibt's?",
            "Moin!",
            "Huhu!",
            "Na?",
            "Ja?",
            "Lang nicht gesehen!",
            "Schön dich zu sehen!",
            "Moin!",
            "Moin moin!",
            "Guten Tag!",
            "Guten Morgen!",
            "Guten Abend!",
            "Gute Nacht!",
            "Und, schon viral?",
            "Emoji des Tages?",
            "Hashtag Bist Du Das?",
            "Neues Meme, wer das?",
            "Na, du Hashtag Held?",
            "Meme-Master meldet sich!",
            "Selfie-Ready?",
            "Geliked und gesehen!",
            "Was wird.",
            "Was geht?",
            "Was geht ab, Schwester?",
            "Hey, Kquien!",

        };
        private readonly Random _random = new Random();



        private Task _executeTask;
        private readonly CancellationTokenSource _cancelToken = new();

        // Notification sound support
        private readonly string _notificationSoundFilePath;
        private readonly Player _player;

        /// <summary>
        /// Constructor
        /// </summary>
        public HostedService(
            AzCognitiveServicesWakeWordListener wakeWordListener,
            IKernel semanticKernel,
            AzCognitiveServicesSpeechSkill speechSkill,
            IOptions<OpenAiServiceOptions> openAIOptions,
            IOptions<AzureOpenAiServiceOptions> azureOpenAIOptions,
            IOptions<GeneralOptions> generalOptions,
            ILogger<HostedService> logger)
        {
            _logger = logger;
            
            _semanticKernel = semanticKernel;

            // LED and PTT
            #if RASPBERRY_PI
                _statusLed = new RaspberryPiStatusLed();
                _startTrigger = new GpioStartTrigger();
            #else
                _statusLed = new MockStatusLed();
                _startTrigger = new KeyboardStartTrigger();
            #endif

            _startTrigger.Triggered += StartTrigger_Triggered;

            

            // OpenAI
            _chatRequestSettings = new ChatRequestSettings()
            {
                MaxTokens = openAIOptions.Value.MaxTokens,
                Temperature = openAIOptions.Value.Temperature,
                FrequencyPenalty = openAIOptions.Value.FrequencyPenalty,
                PresencePenalty = openAIOptions.Value.PresencePenalty,
                TopP = openAIOptions.Value.TopP,
                StopSequences = new string[] { "\n\n" }
            };
            _semanticKernel.Config.AddOpenAIChatCompletionService(
                openAIOptions.Value.Model, openAIOptions.Value.Key, alsoAsTextCompletion: true, logger: _logger);

            // Azure OpenAI
            //_chatRequestSettings = new ChatRequestSettings()
            //{
            //    MaxTokens = azureOpenAIOptions.Value.MaxTokens,
            //    Temperature = azureOpenAIOptions.Value.Temperature,
            //    FrequencyPenalty = azureOpenAIOptions.Value.FrequencyPenalty,
            //    PresencePenalty = azureOpenAIOptions.Value.PresencePenalty,
            //    TopP = azureOpenAIOptions.Value.TopP,
            //    StopSequences = new string[] { "\n\n" }
            //};
            //_semanticKernel.Config.AddAzureChatCompletionService(
            //    azureOpenAIOptions.Value.Deployment, azureOpenAIOptions.Value.Endpoint, azureOpenAIOptions.Value.Key, alsoAsTextCompletion: true, logger: _logger);

            _wakeWordListener = wakeWordListener;

            _chatCompletion = _semanticKernel.GetService<IChatCompletion>();
            _chatHistory = (OpenAIChatHistory)_chatCompletion.CreateNewChat(generalOptions.Value.SystemPrompt);

            _speechSkill = _semanticKernel.ImportSkill(speechSkill);

            _notificationSoundFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Handlers", "bing.mp3");
            _player = new Player();
        }

        /// <summary>
        /// Start the service.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _executeTask = ExecuteAsync(_cancelToken.Token);
            return Task.CompletedTask;
        }

        /// <summary>
        /// LED and PTT
        /// </summary>
        private void StartTrigger_Triggered(object sender, EventArgs e)
        {
            _isTriggered = true;
        }




        /// <summary>
        /// Primary service logic loop.
        /// </summary>
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _statusLed.Initialize();  // Light up in blue or any idle color.

            while (!cancellationToken.IsCancellationRequested)
            {
                await _player.Play(_notificationSoundFilePath);
                
                // Wait for the start trigger
                await Task.Run(() =>
                {
                    while (!cancellationToken.IsCancellationRequested && !_isTriggered)
                    {
                        Thread.Sleep(100);
                    }
                    _isTriggered = false;
                });

                _statusLed.Listening();  // Change to green or any listening color.
                
                string randomGreeting = _greetings[_random.Next(_greetings.Count)];
                await _semanticKernel.RunAsync(randomGreeting, _speechSkill["Speak"]);

                while (!cancellationToken.IsCancellationRequested)
                {
                    _statusLed.Processing();  // Change to yellow or any processing color.

                    // Your existing logic for capturing user input and getting AI's response...

                    // If there's an error
                    if (/*error condition*/)
                    {
                        _statusLed.Error();  // Flash red or any error indication.
                    }
                    else
                    {
                        _statusLed.ResponseReady();  // Change to cyan or any response color.
                    }
                    
                    // Speak the AI's reply and then reset to idle state
                    await _semanticKernel.RunAsync(reply, _speechSkill["Speak"]);
                    _statusLed.Idle();  // Reset to blue or any idle color.

                    break;
                }
            }
        }

        /// <summary>
        /// Stop a running service.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancelToken.Cancel();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            _cancelToken.Dispose();
            _wakeWordListener.Dispose();
        }
    }
}