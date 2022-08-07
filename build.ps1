dotnet tool install -g GitVersion.Tool --version 5.* -v q
$gitversion = dotnet-gitversion /updateassemblyinfo | ConvertFrom-Json 

msbuild -t:restore /property:Configuration=Release
msbuild /property:Configuration=Release

Install-Package ilmerge -RequiredVersion 3.0.29 -Force -SkipValidate -Scope CurrentUser

$APP_NAME = "subcheck.exe"
$ILMERGE_BUILD = "Release"
$ILMERGE_VERSION = "3.0.29"

$ilmergeArgs = "Bin\$ILMERGE_BUILD\$APP_NAME /lib:Bin\$ILMERGE_BUILD\ /out:$APP_NAME Microsoft.Build.Locator.dll"

$ILMERGE_PATH = "$env:USERPROFILE\.nuget\packages\ilmerge\$ILMERGE_VERSION\tools\net452\ILMerge.exe"
echo $ILMERGE_PATH
echo $ilmergeArgs
Start-Process $ILMERGE_PATH -ArgumentList $ilmergeArgs -Wait
