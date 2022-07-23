dotnet tool install -g GitVersion.Tool --version 5.* -v q
dotnet tool install -g dotnet-setversion -v q
$gitversion = dotnet-gitversion | ConvertFrom-Json 
setversion $gitversion.AssemblySemVer
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true "SubCheck.csproj" 
Copy-Item "./bin/Release/net6.0/win-x64/publish/SubCheck.exe" .\