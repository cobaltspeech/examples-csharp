using CommandLine;
using Grpc.Net.Client;
using Grpc.Core;
using Bluehenge = Cobaltspeech.Bluehenge.V1;
using Diatheke = Cobaltspeech.Diatheke.V3;

public class BluehengeExampleClient
{
    // Command line flag options.
    public class Options
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to include verbose messages.")]
        public bool Verbose { get; set; }

        [Option('s', "server", Required = false, HelpText = "Bluehenge server address.  ex: http://localhost:9000")]
        public bool ServerAddress { get; set; }
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
        var channelOptions = new GrpcChannelOptions{Credentials = ChannelCredentials.Insecure};
        using var channel = GrpcChannel.ForAddress("http://localhost:19007", channelOptions);
        var client = new Bluehenge.BluehengeService.BluehengeServiceClient(channel);

        // Run the simple methods the server provides.
        this.Version(client);
        var firstModel = this.ListModels(client);
    }

    public void Version(Bluehenge.BluehengeService.BluehengeServiceClient grpcClient)
    {
        // Fetch the servers' models.
        var versionReq = new Bluehenge.VersionRequest();
        var versionResp = grpcClient.Version(versionReq);

        // Print the results
        Console.WriteLine(String.Format(
            "Versions: Bluehenge: {0}; Others: {1}",
            versionResp.Bluehenge.ToString(),
            versionResp.DiathekeVersionResponse.ToString()
        ));
    }

    // ListModels will fetch all the models available, print them all out, and then return the first model available.
    public Diatheke.ModelInfo ListModels(Bluehenge.BluehengeService.BluehengeServiceClient grpcClient)
    {
        // Fetch the list of models.
        var req = new Bluehenge.ListModelsRequest();
        var listModelsResp = grpcClient.ListModels(req);

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
}
