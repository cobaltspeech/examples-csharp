using CommandLine;
using Grpc.Net.Client;
using Grpc.Core;
using Bluehenge = Cobaltspeech.Bluehenge.V1;
using Diatheke = Cobaltspeech.Diatheke.V3;
using Cobaltspeech.Diatheke.V3;

public class BluehengeExampleClient
{
    private static IEnumerable<byte[]> ReadFileInChunks(string filePath)
    {
        const int MAX_BUFFER = 8192;
        byte[] buffer = new byte[MAX_BUFFER];

        using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
        using (BufferedStream bs = new BufferedStream(fs))
        {
            var bytesRead = bs.Read(buffer, 0, MAX_BUFFER); //reading one chunks at a time.
            if (bytesRead == 0) // EOF.
            {
                yield break;
            }
            yield return buffer;
        }
    }

    // Command line flag options.
    public class Options
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to include verbose messages.")]
        public bool Verbose { get; set; }

        [Option('s', "server", Required = true, HelpText = "Bluehenge server address.  ex: http://localhost:9000")]
        public String ServerAddress { get; set; }
    }

    static void Main(string[] args)
    {
        // Parse CommandLine flags.
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(o =>
            {
                var client = new BluehengeExampleClient(o);
                client.run();
            });
    }

    private Options opts { set; get; }

    public BluehengeExampleClient(Options options)
    {
        this.opts = options;
        Console.WriteLine("creating client.");
    }

    public void run()
    {
        // Create a gRPC connection.
        var channelOptions = new GrpcChannelOptions { Credentials = ChannelCredentials.Insecure };
        using var channel = GrpcChannel.ForAddress(this.opts.ServerAddress, channelOptions);
        var client = new Bluehenge.BluehengeService.BluehengeServiceClient(channel);

        // Run the simple methods the server provides.
        this.Version(client);
        var firstModel = this.ListModels(client);

        // Now, in a separate function, we'll start into the more interesting stuff related to sessions.
        this.runSession(client);
    }

    public void Version(Bluehenge.BluehengeService.BluehengeServiceClient client)
    {
        // Fetch the servers' models.
        var versionReq = new Bluehenge.VersionRequest();
        var versionResp = client.Version(versionReq);

        // Print the results
        Console.WriteLine(String.Format(
            "Versions: Bluehenge: {0}; Others: {1}",
            versionResp.Bluehenge.ToString(),
            versionResp.DiathekeVersionResponse.ToString()
        ));
    }

    // ListModels will fetch all the models available, print them all out, and then return the first model available.
    public Diatheke.ModelInfo ListModels(Bluehenge.BluehengeService.BluehengeServiceClient client)
    {
        // Fetch the list of models.
        var req = new Bluehenge.ListModelsRequest();
        var listModelsResp = client.ListModels(req);

        // Print out the results.
        Console.WriteLine("Models Available:");
        foreach (var m in listModelsResp.DiathekeListModelsResponse.Models)
        {
            Console.WriteLine(String.Format(
                "\t{0}: {1} ({2}) (ASR rate: {3}; TTS rate: {4})",
                m.Id, m.Name, m.Language, m.AsrSampleRate, m.TtsSampleRate));
        }

        return listModelsResp.DiathekeListModelsResponse.Models[0];
    }

    private async void runSession(Bluehenge.BluehengeService.BluehengeServiceClient client)
    {
        // Create Session
        var sessionResp = client.CreateSession(new Bluehenge.CreateSessionRequest());
        var sessionOutput = sessionResp.DiathekeCreateSessionResponse.SessionOutput;

        // Handle any action items that get returned from the ActionList.
        // This will be our long living item.  We continue to handle responses from diatheke until 
        // there is nothing left for us to do.
        for (; ; )
        {
            // processAction handles one action at a time, and returns us the updated state.
            sessionOutput = await this.processActionAsync(client, sessionOutput);
            if (sessionOutput == null)
            {
                break;
            }
        }

        // Once Diatheke is done with the action list, we'll delete the session and be done.
        var deleteRequest = new Bluehenge.DeleteSessionRequest
        {
            DiathekeDeleteSessionRequest = new Diatheke.DeleteSessionRequest
            {
                TokenData = sessionOutput?.Token
            }
        };
        client.DeleteSession(deleteRequest);
    }

    // processActions executes the actions for the given session and returns an updated session.
    private async Task<SessionOutput> processActionAsync(Bluehenge.BluehengeService.BluehengeServiceClient client, Diatheke.SessionOutput sessionOut)
    {
        // Iterate through each action in the list and determine its type.
        foreach (var action in sessionOut.ActionList)
        {
            switch (action.ActionCase)
            {
                case Diatheke.ActionData.ActionOneofCase.Input:
                    sessionOut = await waitForInputAsync(client, sessionOut, action.Input);
                    break;
                case Diatheke.ActionData.ActionOneofCase.Reply:
                    // Replies are TTS requests and do not require a session update.
                    handleReply(client, action.Reply);
                    break;
                case Diatheke.ActionData.ActionOneofCase.Command:
                    // The CommandAction will involve a session update.
                    sessionOut = await handleCommand(client, sessionOut, action.Command);
                    break;
                case Diatheke.ActionData.ActionOneofCase.Transcribe:
                    // Transcribe actions do not require a session update.
                    handleTranscribe(client, action.Transcribe);
                    break;
                default:
                    throw new Exception("Unknown action type");
            }
        }

        return null;
    }

    // waitForInput prompts the user for text input, then updates the
    // session based on the user-supplied text.
    private async Task<Diatheke.SessionOutput> waitForInputAsync(
        Bluehenge.BluehengeService.BluehengeServiceClient client,
        Diatheke.SessionOutput session, Diatheke.WaitForUserAction inputAction)
    {
        // The given input action has a couple of flags to help the app
        // decide when to begin recording audio.
        if (inputAction.Immediate)
        {
            // This action is likely waiting for user input in response to
            // a question Diatheke asked, in which case the user should
            // reply immediately. If this flag is false, the app may wait
            // as long as it wants before processing user input (such as
            // waiting for a wake-word below).
            Console.WriteLine("(Immediate input required)");
        }

        if (inputAction.RequiresWakeWord)
        {
            // This action requires the wake-word to be spoken before
            // any other audio will be processed. Use a wake-word detector
            // and wait for it to trigger.
            Console.WriteLine("(Wakeword required)");
        }

        // Create an ASR stream.
        // The client will send multiple messages and get a single transcription response back.
        var stream = client.StreamASR();

        #region audio upload
        // First we send up a config message.
        var req = new Bluehenge.StreamASRRequest
        {
            DiathekeStreamAsrRequest = new Diatheke.StreamASRRequest
            {
                Token = session.Token
            }
        };
        stream.RequestStream.WriteAsync(req).Wait();

        // Now we stream the audio until it's done.
        // Note: in this example, we'll pull from an audio file.  Other applications might want to pull from 
        // a microphone or other audio source.
        foreach (var chunk in BluehengeExampleClient.ReadFileInChunks("MyFileName"))
        {
            req = new Bluehenge.StreamASRRequest
            {
                DiathekeStreamAsrRequest = new Diatheke.StreamASRRequest
                {
                    Audio = Google.Protobuf.ByteString.CopyFrom(chunk)
                }
            };
            await stream.RequestStream.WriteAsync(req);
        }
        await stream.RequestStream.CompleteAsync(); // Let the server know we are done streaming.
        #endregion

        // Once we've written out all of the audio bytes, we can check for the response.
        var asrResponse = await stream;

        Console.WriteLine("  ASR Result: " + asrResponse.DiathekeStreamAsrResponse.AsrResult);

        // Update the session with the result.
        var updateResp = await client.UpdateSessionAsync(new Bluehenge.UpdateSessionRequest
        {
            DiathekeUpdateSessionRequest = new Diatheke.UpdateSessionRequest
            {
                SessionInput = new SessionInput
                {
                    Token = session.Token,
                    Asr = asrResponse.DiathekeStreamAsrResponse.AsrResult,
                }
            }
        });
        return updateResp.DiathekeUpdateSessionResponse.SessionOutput;
    }

    private async void handleReply(Bluehenge.BluehengeService.BluehengeServiceClient client,
        Diatheke.ReplyAction reply)
    {
        Console.WriteLine("  Reply: " + reply.Text);

        // Create the TTS stream
        var stream = client.StreamTTS(new Bluehenge.StreamTTSRequest
        {
            DiathekeStreamTtsRequest = new Diatheke.StreamTTSRequest
            {
                ReplyAction = reply
            }
        });

        // Read all the TTS bytes into a file.
        // Other applications might stream this to an audio player.
        string ttsFilename = Path.GetTempFileName();
        BinaryWriter writer = new BinaryWriter(File.OpenWrite(ttsFilename));
        await foreach (var audio in stream.ResponseStream.ReadAllAsync())
        {
            writer.Write(audio.DiathekeStreamTtsResponse.Audio.ToByteArray());
        }
        writer.Flush();
        writer.Close();
    }

    private async Task<Diatheke.SessionOutput> handleCommand(
        Bluehenge.BluehengeService.BluehengeServiceClient client,
        Diatheke.SessionOutput session, Diatheke.CommandAction command)
    {
        Console.WriteLine("  Command:\n");
        Console.WriteLine("    ID: " + command.Id + "\n");
        Console.WriteLine("    Input params: " + command.InputParameters.ToString() + "\n\n");

        ////////////////////////////////////////////////////////
        //                   ! IMPORTANT !                    //
        // This is where the meat of the application will go! //
        ////////////////////////////////////////////////////////

        // Here, we only demonstrate a few commands that are specific to the API
        // TODO: add in items related to entity and procedure requests:
        // ListProcedures
        // GetProcedures
        // SaveNote
        // GetEntityImage
        // GetImage

        // Update the session with the command result
        var result = new Diatheke.CommandResult
        {
            Id = command.Id,
            // OutParameters, Error
        };

        // Update the session with the result.
        var updateResp = await client.UpdateSessionAsync(new Bluehenge.UpdateSessionRequest
        {
            DiathekeUpdateSessionRequest = new Diatheke.UpdateSessionRequest
            {
                SessionInput = new SessionInput
                {
                    Token = session.Token,
                    Cmd = result,
                }
            }
        });
        return updateResp.DiathekeUpdateSessionResponse.SessionOutput;
    }

    private async void handleTranscribe(Bluehenge.BluehengeService.BluehengeServiceClient client,
        Diatheke.TranscribeAction transcribe)
    {
        // Create the transcription stream.
        using var stream = client.Transcribe();

        #region receiving
        StringWriter finalTranscription = new StringWriter();
        var readTask = Task.Run(async () =>
        {
            await foreach (var result1 in stream.ResponseStream.ReadAllAsync())
            {
                var result = result1.DiathekeTranscribeResponse;
                // Print the result on the same line (overwrite current
                // contents). Note that this assumes that stdout is going
                // to a terminal.
                Console.Write(String.Format("\r{0} (confidence: {1})", result.Text, result.Confidence));

                if (result.IsPartial)
                {
                    continue;
                }

                // As this is the final result (non-partial), go to
                // the next line in preparation for the next result.
                Console.Write("\n");

                // Accumulate all non-partial transcriptions here.
                finalTranscription.Write(result.Text);
            }
        });
        #endregion

        #region audio upload
        var sendTask = Task.Run(async () =>
        {
            // First we send up a config message.
            var req = new Bluehenge.TranscribeRequest
            {
                DiathekeTranscribeRequest = new Diatheke.TranscribeRequest
                {
                    Action = transcribe
                }
            };
            stream.RequestStream.WriteAsync(req).Wait();

            // Now we stream the audio until it's done.
            // Note: in this example, we'll pull from an audio file.  Other applications might want to pull from 
            // a microphone or other audio source.
            foreach (var chunk in BluehengeExampleClient.ReadFileInChunks("MyFileName"))
            {
                req = new Bluehenge.TranscribeRequest
                {
                    DiathekeTranscribeRequest = new Diatheke.TranscribeRequest
                    {
                        Audio = Google.Protobuf.ByteString.CopyFrom(chunk)
                    }
                };
                await stream.RequestStream.WriteAsync(req);
            }
            await stream.RequestStream.CompleteAsync(); // Let the server know we are done streaming.
        });
        #endregion

        await sendTask; // Wait for send task to finish.
        await readTask; // Once that one was done, we should be ready to wait for the final messages.

        Console.WriteLine("  Transcription: " + finalTranscription.ToString() + "\n\n");
    }
}
