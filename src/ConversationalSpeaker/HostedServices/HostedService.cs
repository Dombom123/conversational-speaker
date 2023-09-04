using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Device.Gpio;
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

namespace ConversationalSpeaker
{
    internal class HostedService : IHostedService, IDisposable
    {
        private readonly ILogger<HostedService> _logger;
        private readonly IKernel _semanticKernel;
        private readonly IDictionary<string, ISKFunction> _speechSkill;
        private readonly AzCognitiveServicesWakeWordListener _wakeWordListener;
        private readonly IChatCompletion _chatCompletion;
        private readonly OpenAIChatHistory _chatHistory;
        private readonly ChatRequestSettings _chatRequestSettings;
        private readonly GpioController _controller = new GpioController();
        private const int buttonPin = 17;
        private CancellationTokenSource _ledCancellationTokenSource = new CancellationTokenSource();

        private readonly List<string> _greetings = new List<string>
        {
            "Hey!", "Hallo!", "Hi!", "Was gibt's?", "Moin!", "Huhu!", "Na?", "Ja?", 
            "Lang nicht gesehen!", "Schön dich zu sehen!", "Moin!", "Moin moin!", 
            "Guten Tag!", "Guten Morgen!", "Guten Abend!", "Gute Nacht!", "Und, schon viral?", 
            "Emoji des Tages?", "Hashtag Bist Du Das?", "Neues Meme, wer das?", "Na, du Hashtag Held?", 
            "Meme-Master meldet sich!", "Selfie-Ready?", "Geliked und gesehen!", "Was wird.", 
            "Was geht?", "Was geht ab, Schwester?", "Hey, Kquien!"
        };
        private readonly List<string> _jingles = new List<string>
        {
            "Komm zum Boom Konzert am 01.11.2023!",
            "Abonniere Boom auf Spotify und verpasse keinen Beat!",
            "Jetzt Boom auf Apple Music streamen und den Rhythmus spüren!",
            "Kaufe die neue Boom-CD auf Amazon - Lass den Soundtrack deines Lebens beginnen!",
            "Boom - Das ist Musik, die unter die Haut geht!",
            "Boom - Hier ist der Beat, der dein Herz höher schlagen lässt!",
            "Mit Boom wird jede Party zum Hit! Tanze die Nacht durch!",
            "Lass es krachen mit dem neuesten Album von Boom!",
            "Boom - Wo Rhythmus und Melodie zu einem Erlebnis verschmelzen!",
            "Wenn der Bass droppt, weißt du, es ist ein neuer Track von Boom!",
            "Einschalten, aufdrehen, abheben - mit Boom geht die Party erst richtig los!",
            "Boom - Mehr als nur Musik, es ist ein Lebensgefühl!",
            "Erwecke die Nacht mit den Klängen von Boom!",
            "Boom - Dein Soundtrack für jeden Moment!"
        };
        private readonly Random _random = new Random();
        private Task _executeTask;
        private readonly CancellationTokenSource _cancelToken = new();
        private readonly string _notificationSoundFilePath;
        private readonly Player _player;
        private string _lastResponse = string.Empty;
        private bool _shouldRespond = true;

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
            _controller.OpenPin(buttonPin, PinMode.InputPullUp);
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
            _wakeWordListener = wakeWordListener;
            _chatCompletion = _semanticKernel.GetService<IChatCompletion>();
            _chatHistory = (OpenAIChatHistory)_chatCompletion.CreateNewChat(generalOptions.Value.SystemPrompt);
            _speechSkill = _semanticKernel.ImportSkill(speechSkill);
            _notificationSoundFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Handlers", "bing.mp3");
            _player = new Player();
            _ = ReadCommandsAsync(_cancelToken.Token);
        }

        private async Task ReadCommandsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var input = Console.ReadLine();
                if (!string.IsNullOrEmpty(input))
                {
                    await HandleCommand(input);
                }
                await Task.Delay(100); // Prevent a tight loop
            }
        }


        private async Task HandleCommand(string command)

        {
            try
            {
                if (command.StartsWith("setprompt "))
                {
                    var newPrompt = command.Substring("setprompt ".Length);
                    Console.WriteLine($"System prompt set to: {newPrompt}");
                }
                else if (command == "clear")
                {
                    _chatHistory.Messages.Clear();
                    Console.WriteLine("Chat log cleared.");
                }
                else if (command == "exit")
                {
                    Console.WriteLine("Exiting program...");
                    _cancelToken.Cancel();
                    Environment.Exit(0);
                }
                else if (command == "greet")
                {
                    var randomGreeting = _greetings[_random.Next(_greetings.Count)];
                    await _semanticKernel.RunAsync(randomGreeting, _speechSkill["Speak"]);
                }
                else if (command == "stop")
                {
                    _shouldRespond = !_shouldRespond;
                    if (_shouldRespond)
                        Console.WriteLine("Resuming responses.");
                    else
                        Console.WriteLine("Stopped responding.");
                }
                else if (command == "repeat")
                {
                    if (!string.IsNullOrEmpty(_lastResponse))
                    {
                        await _semanticKernel.RunAsync(_lastResponse, _speechSkill["Speak"]);
                    }
                    else
                    {
                        Console.WriteLine("No previous response to repeat.");
                    }
                }
                else
                {
                    Console.WriteLine("Unknown command.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command: {ex.Message}");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _executeTask = ExecuteAsync(_cancelToken.Token);
            return Task.CompletedTask;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ControlLED("idle");
                while (_controller.Read(buttonPin) == PinValue.High)
                {
                    await Task.Delay(100, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                        return;
                }
                ControlLED("listening");
                await _semanticKernel.RunAsync(_speechSkill["StartListening"]);
                while (_controller.Read(buttonPin) == PinValue.Low)
                {
                    await Task.Delay(100, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                        return;
                }
                ControlLED("thinking");
                SKContext context = await _semanticKernel.RunAsync(_speechSkill["StopListening"]);
                string userSpoke = context.Result;
                string reply = string.Empty;
                if (_shouldRespond)
                {
                    try
                    {
                        _chatHistory.AddUserMessage(userSpoke);
                        _lastResponse = await _chatCompletion.GenerateMessageAsync(_chatHistory, _chatRequestSettings);
                        _chatHistory.AddAssistantMessage(_lastResponse);
                    }
                    catch (AIException aiex)
                    {
                        _logger.LogError($"OpenAI returned an error. {aiex.ErrorCode}: {aiex.Message}");
                        _lastResponse = "OpenAI returned an error. Please try again.";
                    }
                    // Logic to append a jingle to the chatbot's response 1/3 of the time
                    if (_random.NextDouble() < 1.0/3.0)
                    {
                        _lastResponse += "\n\nWerbung...\n\n" + _jingles[_random.Next(_jingles.Count)];
                    }
                    ControlLED("responding");
                    await _semanticKernel.RunAsync(_lastResponse, _speechSkill["Speak"]);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancelToken.Cancel();
            return Task.CompletedTask;
        }

        public virtual void Dispose()
        {
            _cancelToken.Dispose();
            _wakeWordListener.Dispose();
        }

        private void ControlLED(string command)
        {
            // Cancel any ongoing LED animations
            _ledCancellationTokenSource.Cancel();
            _ledCancellationTokenSource.Dispose();
            _ledCancellationTokenSource = new CancellationTokenSource();
            
            Task.Run(() =>
            {
                try
                {
                    foreach (var process in Process.GetProcessesByName("python3"))
                    {
                        if (process.StartInfo.Arguments.Contains("led_controller.py"))
                        {
                            process.Kill();
                            process.WaitForExit();
                        }
                    }
                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = "sudo";
                        process.StartInfo.Arguments = $"python3 /home/pi/Documents/code/conversational-speaker/src/ConversationalSpeaker/led_controller.py --action {command}";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.Start();
                        string output = process.StandardOutput.ReadToEnd();
                        string errors = process.StandardError.ReadToEnd();
                        if (!string.IsNullOrEmpty(errors))
                        {
                            _logger.LogError(errors);
                        }
                        process.WaitForExit();
                    }
                }
                catch (OperationCanceledException)
                {
                    // This exception is expected when the task gets canceled, so no need to handle it
                }
            }, _ledCancellationTokenSource.Token);
        }

    }
}
