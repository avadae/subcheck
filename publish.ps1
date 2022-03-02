dotnet publish -r win-x64 -p:PublishTrimmed=True -p:TrimMode=CopyUsed -c Release /p:PublishSingleFile=true "SubCheck.csproj" 
Copy-Item ".\bin\Release\net5.0\win-x64\publish\SubCheck.exe" .\