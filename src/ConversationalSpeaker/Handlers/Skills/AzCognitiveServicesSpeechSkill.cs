using System.Text.RegularExpressions;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using System.Text;  // Required for StringBuilder

namespace ConversationalSpeaker
{
    public class AzCognitiveServicesSpeechSkill : IDisposable
    {
        private readonly ILogger _logger;
        private readonly AzureCognitiveServicesOptions _options;
        private readonly AudioConfig _audioConfig;
        private readonly SpeechRecognizer _speechRecognizer;
        private readonly SpeechSynthesizer _speechSynthesizer;

        // Add these class-level variables to manage the state of speech recognition:
        private readonly StringBuilder _recognizedText = new StringBuilder();
        private bool _isRecognizing = false;

        /// <summary>
        /// Regex for extracting style cues from OpenAI responses.
        /// (not currently supported after the migrations to ChatGPT models)
        /// </summary>
        private static readonly Regex _styleRegex = new Regex(@"(~~(.+)~~)");

        public AzCognitiveServicesSpeechSkill(
            IOptions<AzureCognitiveServicesOptions> options,
            ILogger<AzCognitiveServicesSpeechSkill> logger)
        {
            _logger = logger;
            _options = options.Value;
            _options.Validate();

            _audioConfig = AudioConfig.FromDefaultMicrophoneInput();

            SpeechConfig speechConfig = SpeechConfig.FromSubscription(_options.Key, _options.Region);
            speechConfig.SpeechRecognitionLanguage = _options.SpeechRecognitionLanguage;
            speechConfig.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");
            speechConfig.SpeechSynthesisVoiceName = _options.SpeechSynthesisVoiceName;

            _speechRecognizer = new SpeechRecognizer(speechConfig, _audioConfig);           
            _speechSynthesizer = new SpeechSynthesizer(speechConfig);
        }

        // Buffer to store the complete recognized text.
        private StringBuilder _finalRecognizedText = new StringBuilder();

        [SKFunction("Start the microphone and perform continuous speech-to-text.")]
        [SKFunctionName("StartListening")]
        public async Task StartListeningAsync(SKContext context)
        {
            _recognizedText.Clear();
            _finalRecognizedText.Clear();

            _speechRecognizer.Recognizing += OnRecognizing;
            _speechRecognizer.Recognized += OnRecognized;

            _isRecognizing = true;
            await _speechRecognizer.StartContinuousRecognitionAsync();
        }

        private void OnRecognizing(object sender, SpeechRecognitionEventArgs e)
        {
            _recognizedText.Clear();
            _recognizedText.Append(e.Result.Text);
        }

        private void OnRecognized(object sender, SpeechRecognitionEventArgs e)
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                _finalRecognizedText.Append(e.Result.Text);
                _recognizedText.Clear();  // Clear the intermediate buffer after appending to the final text
            }
        }

        [SKFunction("Stop the microphone and return the recognized text.")]
        [SKFunctionName("StopListening")]
        public async Task<string> StopListeningAsync(SKContext context)
        {
            if (!_isRecognizing)
                return string.Empty;

            _isRecognizing = false;
            await _speechRecognizer.StopContinuousRecognitionAsync();

            // Unsubscribe from the events to prevent multiple triggers
            _speechRecognizer.Recognizing -= OnRecognizing;
            _speechRecognizer.Recognized -= OnRecognized;

            _logger.LogInformation($"Recognized: {_finalRecognizedText.ToString()}");
            return _finalRecognizedText.ToString();
        }



        [SKFunction("Speak the current context (text-to-speech).")]
        [SKFunctionName("Speak")]
        public async Task SpeakAsync(string message, SKContext context)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                // Parse speaking style, if any
                message = ExtractStyle(message, out string style);
                if (string.IsNullOrWhiteSpace(style))
                {
                    _logger.LogInformation($"Speaking (none): {message}");
                }
                else
                {
                    _logger.LogInformation($"Speaking ({style}): {message}");
                }

                string ssml = GenerateSsml(
                    message,
                    _options.EnableSpeechStyle ? style : string.Empty,
                    _options.SpeechSynthesisVoiceName);

                _logger.LogDebug(ssml);
                await _speechSynthesizer.SpeakSsmlAsync(ssml);
            }
        }

        /// <summary>
        /// Extract style cues from a message.
        /// </summary>
        private string ExtractStyle(string message, out string style)
        {
            style = string.Empty;
            Match match = _styleRegex.Match(message);
            if (match.Success)
            {
                style = match.Groups[2].Value.Trim();
                message = message.Replace(match.Groups[1].Value, string.Empty).Trim();
            }
            return message;
        }

        /// <summary>
        /// Generate speech synthesis markup language (SSML) from a message.
        /// </summary>
        private string GenerateSsml(string message, string style, string voiceName)
            => "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"https://www.w3.org/2001/mstts\" xml:lang=\"en-US\">" +
                $"<voice name=\"{voiceName}\">" +
                    $"<prosody rate=\"{_options.Rate}\">" +
                        $"<mstts:express-as style=\"{style}\">" +
                            $"{message}" +
                        "</mstts:express-as>" +
                    "</prosody>" +
                    "</voice>" +
                "</speak>";

        public void Dispose()
        {
            _speechRecognizer.Dispose();
            _audioConfig.Dispose();
        }
    }
}
