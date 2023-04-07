This folder contains an example console project, demonstrating the most basic connection to a Bluehenge server.

Computer setup was done via https://code.visualstudio.com/docs/languages/dotnet
Project creation was done via `dotnet new console`.

You will need to download the protobuf files before the project will compile.
This is done with the command `dotnet build -t:DownloadProtobufFiles`.

Now you can compile into a binary via `dotnet build` or run directly via `dotnet run --server http://localhost:9000`.
