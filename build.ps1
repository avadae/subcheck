dotnet tool install -g GitVersion.Tool --version 5.* -v q
$GITVERSION = dotnet-gitversion /updateassemblyinfo | ConvertFrom-Json 

echo $GITVERSION

dotnet msbuild /property:Configuration=Release

$APP_NAME = "subcheck.exe"
$ILMERGE_VERSION = "3.0.29"
$ILMERGE_BUILD = "Release"
$ILMERGE_ARGS = "Bin\$ILMERGE_BUILD\$APP_NAME /lib:Bin\$ILMERGE_BUILD\ /out:$APP_NAME Microsoft.Build.Locator.dll"

Install-Package ilmerge -RequiredVersion $ILMERGE_VERSION -Force -SkipValidate -Scope CurrentUser
$pkg = Get-Package ilmerge -RequiredVersion $ILMERGE_VERSION
$ILMERGE_PATH = Join-Path $pkg.Source "..\tools\net452\ILMerge.exe"

Start-Process $ILMERGE_PATH -ArgumentList $ILMERGE_ARGS -Wait