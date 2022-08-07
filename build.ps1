dotnet tool install -g GitVersion.Tool --version 5.* -v q
$gitversion = dotnet-gitversion /updateassemblyinfo | ConvertFrom-Json 

msbuild SubCheck.csproj -t:restore /property:Configuration=Release

msbuild SubCheck.csproj /property:Configuration=Release

Install-Package ilmerge -RequiredVersion 3.0.29 -Force -SkipValidate -Scope CurrentUser
#$gitversion = dotnet-gitversion | ConvertFrom-Json 
#setversion $gitversion.AssemblySemVer subcheck.csproj

$APP_NAME = "subcheck.exe"
$ILMERGE_BUILD = "Release"
$ILMERGE_VERSION = "3.0.29"

$ilmergeArgs = "Bin\$ILMERGE_BUILD\$APP_NAME /lib:Bin\$ILMERGE_BUILD\ /out:$APP_NAME Microsoft.Build.Locator.dll"

$ILMERGE_PATH = "$env:USERPROFILE\.nuget\packages\ilmerge\$ILMERGE_VERSION\tools\net452\ILMerge.exe"
echo $ILMERGE_PATH
echo $ilmergeArgs
Start-Process $ILMERGE_PATH -ArgumentList $ilmergeArgs -Wait

#dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true "SubCheck.csproj" 
#Copy-Item "./bin/Release/net6.0/win-x64/publish/SubCheck.exe" .\