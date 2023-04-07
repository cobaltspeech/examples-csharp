using CommandLine;
using Grpc.Net.Client;
using Grpc.Core;
using Bluehenge = Cobaltspeech.Bluehenge.V2;
using Diatheke = Cobaltspeech.Diatheke.V3;
using System.Runtime.CompilerServices;

public class BluehengeExampleClient
{
    private static readonly string AUDIO_FILE_PATH = "audiofiles/ok_us8bit.wav";

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
        public bool Verbose { get; set; } = false;

        [Option('s', "server", Required = true, HelpText = "Bluehenge server address.  ex: http://localhost:9000")]
        public String ServerAddress { get; set; } = "http://localhost:9000";
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

        Console.WriteLine("thanks for playing!");
    }

    private Options opts { set; get; }

    public BluehengeExampleClient(Options options)
    {
        this.opts = options;
    }

    public void run()
    {
        // Create a gRPC connection.
        Console.WriteLine("creating gRPC channel.");
        var channelOptions = new GrpcChannelOptions { Credentials = ChannelCredentials.Insecure };
        using var channel = GrpcChannel.ForAddress(this.opts.ServerAddress, channelOptions);
        Console.WriteLine("creating client.");
        var client = new Bluehenge.BluehengeService.BluehengeServiceClient(channel);

        // Run the simple methods the server provides.
        this.Version(client);
        var firstModel = this.ListModels(client);

        // Now, in a separate function, we'll start into the more interesting stuff related to sessions.
        // Note: runSession is defined as an async function.  We'll need to wait for that to finish.  
        //       But since run() is not an async function, we cannot use await this.runSession.
        //       So, we call task.Wait().
        this.runSession(client, firstModel).Wait();
        Console.WriteLine("runSession finished");
    }

    public void Version(Bluehenge.BluehengeService.BluehengeServiceClient client)
    {
        Console.WriteLine("calling Version =================");
        // Fetch the servers' models.
        var versionReq = new Bluehenge.VersionRequest();
        var versionResp = client.Version(versionReq);

        // Print the results
        Console.WriteLine(String.Format(
            "Versions: Bluehenge: {0};\n          Others: {1}",
            versionResp.Bluehenge.ToString(),
            versionResp.DiathekeVersionResponse.ToString()
        ));
    }

    // ListModels will fetch all the models available, print them all out, and then return the first model available.
    public Diatheke.ModelInfo ListModels(Bluehenge.BluehengeService.BluehengeServiceClient client)
    {
        Console.WriteLine("calling ListModels ==============");
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

    private async Task runSession(Bluehenge.BluehengeService.BluehengeServiceClient client, Diatheke.ModelInfo model)
    {
        // Create Session
        Console.WriteLine(String.Format(
            "runSession({0}) =============", model.Id
        ));
        Bluehenge.CreateSessionRequest sessionReq = new Bluehenge.CreateSessionRequest()
        {
            DiathekeCreateSessionRequest = new Diatheke.CreateSessionRequest() { ModelId = model.Id },
        };
        var sessionResp = await client.CreateSessionAsync(sessionReq);
        var sessionOutput = sessionResp.DiathekeCreateSessionResponse.SessionOutput;
        Console.WriteLine("got createSession back");

        // We will also download the procedure list data
        var procedureList = client.ListProcedures(new Bluehenge.ListProceduresRequest{});
        Console.WriteLine("Procedure List Data: ({0})", procedureList.Procedures.Count());
        // Sort by ProcedureName
        var pList = procedureList.Procedures.OrderBy(item => item.ProcedureNumber).ToList();
        foreach (var p in pList)
        {
            Console.WriteLine(String.Format("Procedure({0}) {1}: {2}", p.Id, p.ProcedureNumber, p.ProcedureName));
        }

        // Handle any action items that get returned from the ActionList.
        // This will be our long living application function.  We continue to handle 
        // responses from diatheke until there is nothing left for us to do.
        for (; ; )
        {
            Console.WriteLine("Processing Actions");
            // processAction handles one action at a time, and returns us the updated state.
            sessionOutput = await this.processActionAsync(client, sessionOutput);
            if (sessionOutput == null)
            {
                break;
            }
        }

        // Once Diatheke is done with the action list, we'll delete the session and be done.
        Console.WriteLine("Deleting session.");
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
    private async Task<Diatheke.SessionOutput> processActionAsync(Bluehenge.BluehengeService.BluehengeServiceClient client, Diatheke.SessionOutput sessionOut)
    {
        // Iterate through each action in the list and determine its type.
        foreach (var action in sessionOut.ActionList)
        {
            Console.WriteLine(String.Format("\tAction: {0}", action.ActionCase));
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

        return sessionOut;
    }

    private void Debug(string msg, [CallerLineNumber] int lineNum = 0)
    {
        Console.WriteLine($"'#: {lineNum}  M: {msg}");
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
            Console.WriteLine("\t\t(Immediate input required)");
        }

        if (inputAction.RequiresWakeWord)
        {
            // This action requires the wake-word to be spoken before
            // any other audio will be processed. Use a wake-word detector
            // and wait for it to trigger.
            Console.WriteLine("\t\t(Wakeword required)");
        }

        var sessionOutput = await waitForTextInput(client, session);
        return sessionOutput;

        // return await recordAndSendAudio(client, session); // TODO: fix audio streaming.
        // for now, we'll just do text based IO
        // var updateResp = await client.UpdateSessionAsync(new Bluehenge.UpdateSessionRequest
        //     {
        //         DiathekeUpdateSessionRequest = new Diatheke.UpdateSessionRequest
        //         {
        //             SessionInput = new SessionInput
        //             {
        //                 Token = session.Token,
        //                 Text = new TextInput {
        //                     Text = "Start Procedure"
        //                 }
        //             }
        //         }
        //     });
        // return updateResp.DiathekeUpdateSessionResponse.SessionOutput;
    }

    private async Task<Diatheke.SessionOutput> waitForTextInput(Bluehenge.BluehengeService.BluehengeServiceClient client,
        Diatheke.SessionOutput session)
    {
        Console.Write("Please Input your text: >");
        var userInput = Console.ReadLine();

        // Update the session with the result.
        var updateReq = new Bluehenge.UpdateSessionRequest
        {
            DiathekeUpdateSessionRequest = new Diatheke.UpdateSessionRequest
            {
                SessionInput = new Diatheke.SessionInput
                {
                    Token = session.Token,
                    Text = new Diatheke.TextInput
                    {
                        Text = userInput,
                    },
                }
            }
        };
        var updateResp = await client.UpdateSessionAsync(updateReq);
        var sessionOutput = updateResp.DiathekeUpdateSessionResponse.SessionOutput;
        return sessionOutput;
    }


    private async Task<Diatheke.SessionOutput> recordAndSendAudio(Bluehenge.BluehengeService.BluehengeServiceClient client,
        Diatheke.SessionOutput session)
    {
        // Create an ASR stream.
        // The client will send multiple messages and get a single transcription response back.
        using (var stream = client.StreamASR())
        {
            #region audio upload
            // First we send up a config message.
            var req = new Bluehenge.StreamASRRequest
            {
                DiathekeStreamAsrRequest = new Diatheke.StreamASRRequest
                {
                    Token = session.Token
                }
            };
            await stream.RequestStream.WriteAsync(req);
            Debug("request stream");
            // Now we stream the audio until it's done.
            // Note: in this example, we'll pull from an audio file.  Other applications might want to pull from 
            // a microphone or other audio source.
            foreach (var chunk in BluehengeExampleClient.ReadFileInChunks(AUDIO_FILE_PATH))
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
            // Normally we'd have to call `await stream.RequestStream.CompleteAsync()` to let the server know
            //  we are done streaming, but 
            Debug("write async");

            Debug("complete async");
            #endregion

            // Once we've written out all of the audio bytes, we can check for the response.
            Bluehenge.StreamASRResponse? asrResponse = null;
            try
            {
                asrResponse = await stream;
                Debug("got response");
            }
            catch (Exception e)
            {
                Debug(e.Message);
                return await Task.FromResult<Diatheke.SessionOutput>(new Diatheke.SessionOutput{});
            }
            Console.WriteLine("  ASR Result: " + asrResponse?.DiathekeStreamAsrResponse.AsrResult);

            // Update the session with the result.
            var updateResp = await client.UpdateSessionAsync(new Bluehenge.UpdateSessionRequest
            {
                DiathekeUpdateSessionRequest = new Diatheke.UpdateSessionRequest
                {
                    SessionInput = new Diatheke.SessionInput
                    {
                        Token = session.Token,
                        Asr = asrResponse?.DiathekeStreamAsrResponse?.AsrResult,
                    }
                }
            });
            return updateResp.DiathekeUpdateSessionResponse.SessionOutput;
        }
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
        await foreach (var resp in stream.ResponseStream.ReadAllAsync())
        {
            writer.Write(resp.Audio.ToByteArray());
        }

        writer.Flush();
        writer.Close();
    }

    private string procedureID = "";
    private string taskNumber = "1";

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
        switch (command.Id)
        {
            case "set_procedure_id":
                // Now we would have to call getprocedure to get the actual content of that procedure.
                var procedureDetails = client.GetProcedure(new Bluehenge.GetProcedureRequest{Name = command.InputParameters["procedure_name"]});
                Console.WriteLine("Procedure Details:" + procedureDetails);
                // And then load PDFs or other assets available.
                break;

            default:
                Console.WriteLine("Still need to implement case for "+command.Id);
                break;
        }

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
                SessionInput = new Diatheke.SessionInput
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
            foreach (var chunk in BluehengeExampleClient.ReadFileInChunks(AUDIO_FILE_PATH))
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
