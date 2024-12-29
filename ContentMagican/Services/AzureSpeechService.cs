//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Microsoft.CognitiveServices.Speech;
//using Microsoft.CognitiveServices.Speech.Audio;
//using Microsoft.Extensions.Configuration;

//public class AzureSpeechService
//{
//    private readonly string _subscriptionKey;
//    private readonly string _region;

//    public AzureSpeechService(IConfiguration configuration)
//    {
//        _subscriptionKey = configuration["AzureSpeechCredentials:Key"];
//        _region = configuration["AzureSpeechCredentials:Region"];

//        if (string.IsNullOrEmpty(_subscriptionKey))
//            throw new ArgumentNullException(nameof(_subscriptionKey), "Azure Speech key must not be null or empty.");

//        if (string.IsNullOrEmpty(_region))
//            throw new ArgumentNullException(nameof(_region), "Azure Speech region must not be null or empty.");
//    }

//    public async Task<byte[]> SynthesizeSpeechAsync(string text, string voiceName= "en-US-BrandonNeural")
//    {
//        var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
//        speechConfig.SpeechSynthesisVoiceName = voiceName;
//        speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio48Khz192KBitRateMonoMp3);
//        var wordBoundaries = new List<(string Word, TimeSpan Timestamp)>();

//        var callback = new MemoryAudioCallback();
//        using var pushStream = new PushAudioOutputStream(callback);
//        using var audioConfig = AudioConfig.FromStreamOutput(pushStream);
//        using var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig);


//        var result = await synthesizer.SpeakTextAsync(text);

//        if (result.Reason != ResultReason.SynthesizingAudioCompleted)
//        {
//            if (result.Reason == ResultReason.Canceled)
//            {
//                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
//                throw new Exception($"Speech synthesis canceled. Reason: {cancellation.Reason}, Details: {cancellation.ErrorDetails}");
//            }
//            else
//            {
//                throw new Exception($"Speech synthesis failed. Reason: {result.Reason}");
//            }
//        }

//        var audioData = callback.GetAudioData();
//        return audioData;
//    }

//    public class MemoryAudioCallback : PushAudioOutputStreamCallback
//    {
//        private readonly MemoryStream _memoryStream = new MemoryStream();
//        public override uint Write(byte[] dataBuffer)
//        {
//            _memoryStream.Write(dataBuffer, 0, dataBuffer.Length);
//            return (uint)dataBuffer.Length;
//        }

//        public override void Close()
//        {
//            _memoryStream.Seek(0, SeekOrigin.Begin);
//        }

//        public byte[] GetAudioData() => _memoryStream.ToArray();
//    }
//}
