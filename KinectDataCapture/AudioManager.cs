using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Microsoft.Kinect;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
//using System.Speech.Recognition;
//using System.Speech;
//using System.Speech.AudioFormat;



namespace KinectDataCapture
{
    class AudioManager
    {

        private KinectSensor kinectSensor;
        private KinectAudioSource kinectSource;
        private SpeechRecognitionEngine sre;
        private const string RecognizerId = "SR_MS_en-US_Kinect_10.0";
        private MainWindow mainWindow;
        private bool paused = false;
        private bool valid = false;
        public Choices words;
        double curAudioAngle = 0;


        public AudioManager(MainWindow mainWindow_arg)
        {


            mainWindow = mainWindow_arg;
            mainWindow.UIUpdateAppStatus("AudioManager");

            RecognizerInfo ri = SpeechRecognitionEngine.InstalledRecognizers().Where(r => r.Id == RecognizerId).FirstOrDefault();
            if (ri == null)
                return;

            mainWindow.UIUpdateAppStatus("Creating sre");

            sre = new SpeechRecognitionEngine(ri.Id);
            //sre = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"));

            //mainWindow.updateAppStatus("Dictation grammar");

            //DictationGrammar dictation = new DictationGrammar();
            //dictation.Name = "dictation";
            //dictation.Enabled = true;

            //sre.LoadGrammar(dictation);

            //mainWindow.updateAppStatus("Loaded dictation grammar");


            
            words = new Choices();
            words.Add("data");

            VocabularyParser parser = new VocabularyParser();
            ArrayList parsedWords = parser.parseFile("aviation.txt");

            for (int i = 0; i < parsedWords.Count; i++) {
                string newWord = (string)parsedWords[i];
                words.Add(newWord);
            }

            var gb = new GrammarBuilder();
            //Specify the culture to match the recognizer in case we are running in a different culture.                                 
            gb.Culture = ri.Culture;
            gb.Append(words);

            // Create the actual Grammar instance, and then load it into the speech recognizer.
            var g = new Grammar(gb);

            sre.LoadGrammar(g);

            sre.SpeechRecognized += SreSpeechRecognized;
            sre.SpeechHypothesized += SreSpeechHypothesized;
            sre.SpeechRecognitionRejected += SreSpeechRecognitionRejected;
            sre.AudioStateChanged += new EventHandler<AudioStateChangedEventArgs>(sre_AudioStateChanged);
            

            var t = new Thread(StartDMO);
            t.Start();

            valid = true;


        }


        public bool IsValid()
        {
            return valid;
        }

        private void StartDMO()
        {
            kinectSource = kinectSensor.AudioSource;
            kinectSource.EchoCancellationMode = EchoCancellationMode.CancellationOnly;
            kinectSource.AutomaticGainControlEnabled = false;
            kinectSource.BeamAngleMode = BeamAngleMode.Adaptive;
            var kinectStream = kinectSource.Start();

            sre.SetInputToDefaultAudioDevice();

            sre.SetInputToAudioStream(kinectStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));

            

            //SpeechAudioFormatInfo formatInfo = new System.Speech.AudioFormat.SpeechAudioFormatInfo(44100, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
            
            //sre.SetInputToAudioStream(kinectStream, formatInfo);
            
            sre.RecognizeAsync(RecognizeMode.Multiple);
            kinectSource.BeamAngleChanged += source_BeamChanged;
        }

        public void Stop()
        {
            if (sre != null)
            {
                sre.RecognizeAsyncCancel();
                sre.RecognizeAsyncStop();
                
            }
        }


        void sre_AudioStateChanged(object sender, AudioStateChangedEventArgs e)
        {
            AudioState newState = e.AudioState;
            if (newState == AudioState.Silence) {
                mainWindow.updateAudioState("silence,,");
            }
            else if (newState == AudioState.Speech) {
                mainWindow.updateAudioState("speech," + curAudioAngle);
            }
            else if (newState == AudioState.Stopped) {
                mainWindow.updateAudioState("stopped," + curAudioAngle);
            }

        }

        void source_BeamChanged(object sender, BeamAngleChangedEventArgs e)
        {
            curAudioAngle = e.Angle;
            mainWindow.updateAudioAngle(e.Angle + "");
        }

        void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            //do nothing for now
        }

        void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            //mainWindow.updateSpeechRecognized("Hypothesized="+e.Result.Text +","+e.Result.Confidence);
        }

        void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            //mainWindow.updateSpeechRecognized(e.Result.Text+","+e.Result.Confidence);
            if (e.Result != null)
            {
                //DumpRecordedAudio(e.Result.Audio);
            }

        }

        void DumpRecordedAudio(RecognizedAudio audio)
        {
            if (audio == null) return;

            int fileId = 0;
            string filename;
            while (File.Exists((filename = "RetainedAudio_" + fileId + ".wav")))
                fileId++;

            Console.WriteLine("\nWriting file: {0}", filename);
            using (var file = new FileStream(filename, System.IO.FileMode.CreateNew))
                audio.WriteToWaveStream(file);
        }

    }
}
