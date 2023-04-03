This folder contains an example console project, demonstrating the most basic connection to a Bluehenge server.

Computer setup was done via https://code.visualstudio.com/docs/languages/dotnet
Project creation was done via `dotnet new console`.

You might have to run `dotnet build -t:DownloadProtobufFiles` first.

If your computer is set up correctly, you should be able to cd into this directory, and then run `dotnet run --server http://localhost:9000`.
