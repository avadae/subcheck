dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true "subcheck.csproj" 
Copy-Item ".\bin\Release\net5.0\win-x64\publish\subcheck.exe" .\