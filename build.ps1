dotnet tool install -g GitVersion.Tool --version 5.* -v q
$GITVERSION = dotnet-gitversion /updateassemblyinfo | ConvertFrom-Json 

msbuild /property:Configuration=Release

$ILMERGE_VERSION = "3.0.29"
Install-Package ilmerge -RequiredVersion $ILMERGE_VERSION -Force -SkipValidate -Scope CurrentUser

$APP_NAME = "subcheck.exe"
$ILMERGE_BUILD = "Release"
$ILMERGE_ARGS = "Bin\$ILMERGE_BUILD\$APP_NAME /lib:Bin\$ILMERGE_BUILD\ /out:$APP_NAME Microsoft.Build.Locator.dll"
$ILMERGE_PATH = "$env:USERPROFILE\.nuget\packages\ilmerge\$ILMERGE_VERSION\tools\net452\ILMerge.exe"
Start-Process $ILMERGE_PATH -ArgumentList $ILMERGE_ARGS -Wait