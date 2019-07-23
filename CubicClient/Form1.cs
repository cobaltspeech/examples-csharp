using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// This is the code for your desktop app.
// Press Ctrl+F5 (or go to Debug > Start Without Debugging) to run your app.

namespace CubicClient
{
    public partial class Form1 : Form
    {
        private CobaltSpeech.Cubic.Cubic.CubicClient client;
        private List<CobaltSpeech.Cubic.Model> cubicModels;
        public Form1()
        {
            InitializeComponent();
            this.textBoxURL.Text = "demo-cubic.cobaltspeech.com:2727";
            this.checkBoxSecureConnection.Checked = true;
            this.comboBox1.Enabled = false;
            this.buttonTranscribe.Enabled = false;
        }

        private void ButtonConnect_Click(object sender, EventArgs e)
        {
            if (client != null)
            {
                // We want to disconnect
                this.buttonConnect.Text = "Connect"; // They can click again to connect
                this.textBoxURL.Enabled = true; // We are dropping a connection, so they can change this
                this.checkBoxSecureConnection.Enabled = true; // And change this.
                this.buttonTranscribe.Enabled = false;// But there is no longer a need to transcribe

                // Clear out the combo box
                this.comboBox1.Items.Clear();
                this.comboBox1.Enabled = false;

                // Close down the session
                cubicModels = null;
                client = null;
            }
            else
            {
                this.buttonConnect.Text = "Disconnect";
                this.textBoxURL.Enabled = false;
                this.checkBoxSecureConnection.Enabled = false;
                this.buttonTranscribe.Enabled = true;

                // TODO setup gRPC stuff
                var url = textBoxURL.Text;

                var creds = Grpc.Core.ChannelCredentials.Insecure;
                if (this.checkBoxSecureConnection.Checked)
                {
                    creds = new Grpc.Core.SslCredentials();
                }
                Grpc.Core.Channel channel = new Grpc.Core.Channel(url, creds);
                client = new CobaltSpeech.Cubic.Cubic.CubicClient(channel);

                // Fetch the Models
                var listModelsRequest = new CobaltSpeech.Cubic.ListModelsRequest();
                var models = client.ListModels(listModelsRequest);
                cubicModels = new List<CobaltSpeech.Cubic.Model> { };
                this.comboBox1.Enabled = true;
                foreach (var m in models.Models)
                {
                    this.comboBox1.Items.Add(m.Name);
                    cubicModels.Add(m);
                }

                // Fetch the Version
                var versions = client.Version(new Google.Protobuf.WellKnownTypes.Empty());
                this.labelVersion.Text = String.Format("Versions: CubicSvr {0}, Cubic {1}", versions.Server, versions.Cubic);
            }
        }

        private void ButtonChangeFile_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog1.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                this.textBoxFile.Text = file;
                this.buttonTranscribe.Enabled = true;
            }
        }

        private void ButtonTranscribe_Click(object sender, EventArgs e)
        {
            this.textBoxResults.Text = ""; // Clear the text box from the prvious run.
            LaunchStream();
        }

        private async void LaunchStream()
        {

            // Setup the gRPC connection for BiDi streaming.
            var call = client.StreamingRecognize();

            using (call)
            {
                // Setup Recieve task
                var responseReaderTask = Task.Run(async () =>
                {
                // Wait for the next response
                while (await call.ResponseStream.MoveNext())
                    {
                    // Grab the current response.
                    var response = call.ResponseStream.Current;
                        foreach (var result in response.Results)
                        {
                        // Throw away partial results
                        if (!result.IsPartial)
                            {
                            // Throw away empty responses (silent frames)
                            if (result.Alternatives.Count > 0)
                                {
                                    // Write them out to the text box.
                                    SetText(result.Alternatives[0].Transcript);
                                    SetText(Environment.NewLine);
                                }
                            }
                        }
                    }
                });


                // Send Audio Sections
                {

                    var modelID = cubicModels[comboBox1.SelectedIndex].Id;

                    var request = new CobaltSpeech.Cubic.StreamingRecognizeRequest();
                    var cfg = new CobaltSpeech.Cubic.RecognitionConfig {
                        ModelId = modelID,
                        AudioEncoding = CobaltSpeech.Cubic.RecognitionConfig.Types.Encoding.RawLinear16,
                        EnableWordTimeOffsets = false,
                        EnableRawTranscript = false,
                        EnableConfusionNetwork = false,
                        EnableWordConfidence = false,
                    };
                    request.Config = cfg;
                    await call.RequestStream.WriteAsync(request); // Send the configs

                    // Setup object for streaming audio
                    request.Config = null;
                    request.Audio = new CobaltSpeech.Cubic.RecognitionAudio { };

                    // Send the audio
                    const int chunkSize = 8192;
                    using (var file = File.OpenRead(this.textBoxFile.Text))
                    {
                        int bytesRead;
                        var buffer = new byte[chunkSize];
                        while ((bytesRead = file.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            var bytes = Google.Protobuf.ByteString.CopyFrom(buffer);
                            request.Audio.Data = bytes;
                            await call.RequestStream.WriteAsync(request);
                        }

                        //Close the stream
                        await call.RequestStream.CompleteAsync();
                    }
                }


                // Wait for all of the responses to come back.
                await responseReaderTask;
            }
        }

        delegate void SetTextCallback(string text);
        private void SetText(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.textBoxFile.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBoxResults.AppendText(text);
            }
        }
    }
}