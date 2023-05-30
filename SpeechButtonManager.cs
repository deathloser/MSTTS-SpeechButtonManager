using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Microsoft.CognitiveServices.Speech;
using System.Threading;
using System;
using System.IO;


public class SpeechButtonManager : MonoBehaviour
{
    private class speech_button {
        public int id { get; set;}
        public string script {get; set;}
        public string filename {get; set;}
        public string label_text {get; set;}
        public string sort {get; set;}
    }
    private const string SubscriptionKey = "";
    private const string Region = "westus";

    public GameObject canvas;
    public GameObject mainPanel;
    public GameObject sidePanel;
    public GameObject speechButtonPrefab;
    public TextAsset buttonCsv;
    public AudioSource canvasAudioSource;
    private const int SampleRate = 24000;
    private SpeechSynthesizer synthesizer;
    private SpeechConfig speechConfig;
    private bool audioSourceNeedStop;
    private object threadLocker = new object();
    private bool waitingForSpeak;
    private string message;

    Dictionary<string, int> field_data = new Dictionary<string, int>();
    
    public void speakOnClick(string speechText) {
        lock (threadLocker)
        {
            waitingForSpeak = true;
        }

        string newMessage = null;
        var startTime = DateTime.Now;
        string speakMessage = speechText;
        string textToSpeech = "<speak version='1.0' xmlns='https://www.w3.org/2001/10/synthesis' xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='en-US'><voice name='en-US-JessaNeural'><mstts:express-as type='chat' role ='SeniorFemale'>"+speakMessage+"</mstts:express-as></voice></speak>";
        
        using (var result = synthesizer.SpeakSsmlAsync(textToSpeech).Result)
        {
            var audioDataStream = AudioDataStream.FromResult(result);
            var isFirstAudioChunk = true;
            var audioClip = AudioClip.Create(
                "Speech",
                SampleRate * 600, 
                1,
                SampleRate,
                true,
                (float[] audioChunk) =>
                {
                    var chunkSize = audioChunk.Length;
                    var audioChunkBytes = new byte[chunkSize * 2];
                    var readBytes = audioDataStream.ReadData(audioChunkBytes);
                    if (isFirstAudioChunk && readBytes > 0)
                    {
                        var endTime = DateTime.Now;
                        var latency = endTime.Subtract(startTime).TotalMilliseconds;
                        newMessage = $"Speech synthesis succeeded!\nLatency: {latency} ms.";
                        isFirstAudioChunk = false;
                    }

                    for (int i = 0; i < chunkSize; ++i)
                    {
                        if (i < readBytes / 2)
                        {
                            audioChunk[i] = (short)(audioChunkBytes[i * 2 + 1] << 8 | audioChunkBytes[i * 2]) / 32768.0F;
                        }
                        else
                        {
                            audioChunk[i] = 0.0f;
                        }
                    }

                    if (readBytes == 0)
                    {
                        Thread.Sleep(200); 
                        audioSourceNeedStop = true;
                    }
                });

            canvasAudioSource.clip = audioClip;
            canvasAudioSource.Play();
        }

        lock (threadLocker)
        {
            if (newMessage != null)
            {
                message = newMessage;
            }

            waitingForSpeak = false;
        }
    }

    void Start()
    {
        List<speech_button> buttons = process_csv();
        createSpeechButtons(buttons);
        speechConfig = SpeechConfig.FromSubscription(SubscriptionKey, Region);
        
        speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);

        synthesizer = new SpeechSynthesizer(speechConfig, null);

        synthesizer.SynthesisCanceled += (s, e) =>
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(e.Result);
            message = $"CANCELED:\nReason=[{cancellation.Reason}]\nErrorDetails=[{cancellation.ErrorDetails}]\nDid you update the subscription info?";
        };
        
    }

    void Update()
    {
        lock (threadLocker)
        {
            if (audioSourceNeedStop)
            {
                canvasAudioSource.Stop();
                audioSourceNeedStop = false;
            }
        }
    }

    void OnDestroy()
    {
        if (synthesizer != null)
        {
            synthesizer.Dispose();
        }
    }

    private List<speech_button> process_csv () {
        string[] csvData = buttonCsv.text.Split('\n');
        List<speech_button> speech_buttons = new List<speech_button>();
        foreach (string row in csvData) {
            if (field_data.Count == 0) {
                string[] column_header = row.Split(',');
                for (int i = 0; i < column_header.Length; i++) {
                    field_data[column_header[i].ToLower()] = i;
                }
            } else {
                string[] currentFields = row.Split(',');
                if (currentFields[field_data["id"]].Length > 0) {
                    speech_button currentButton = new speech_button();
                    currentButton.id = int.Parse(currentFields[field_data["id"]].Trim());
                    string buttonLabel = currentFields[field_data["button text"]].Trim();
                    currentButton.label_text = buttonLabel;
                    currentButton.sort = currentFields[field_data["sort"]].Trim();
                    speech_buttons.Add(currentButton);

                }
            }
        }
        return speech_buttons;
    }

    private void createSpeechButtons (List<speech_button> buttons) {
        float verticalPositionMainPanel = 184;
        float horizontalPositionMainPanel = -418;
        float verticalPositionSidePanel = 200;
        float horizontalPositionSidePanel = -168;
        int i = 1;
        foreach (speech_button b in buttons) {
            GameObject speechButtonInstance = (b.sort == "main" ? Instantiate(speechButtonPrefab, mainPanel.transform) : Instantiate(speechButtonPrefab, sidePanel.transform));
            UnityEngine.UI.Button speechButtonComponent = speechButtonInstance.GetComponent<Button>();
            if (b.sort == "main") {
                speechButtonComponent.transform.localPosition = new Vector3(horizontalPositionMainPanel, verticalPositionMainPanel, 0);
                horizontalPositionMainPanel = horizontalPositionMainPanel + 200;
            } 
            if (b.sort == "side") {
                speechButtonComponent.transform.localPosition = new Vector3(horizontalPositionSidePanel, verticalPositionSidePanel, 0);
                verticalPositionSidePanel = verticalPositionSidePanel + 50;
                speechButtonComponent.transform.localScale = new Vector3(4, 1, 1);
            }

            speechButtonComponent.name = b.label_text;
            speechButtonComponent.GetComponentInChildren<TextMeshProUGUI>().text = b.label_text;
            speechButtonComponent.onClick.AddListener(delegate{speakOnClick(b.label_text);});

        }
    }


}
