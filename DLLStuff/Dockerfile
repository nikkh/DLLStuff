#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/windows/servercore:ltsc2019 AS base
RUN powershell Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1';
RUN powershell ./dotnet-install.ps1 -Version "3.1.201" -Architecture x86;
RUN setx path "C:\Users\ContainerAdministrator\AppData\Local\Microsoft\dotnet\;" 
WORKDIR /src 
COPY ["DLLStuff/DLLStuff.csproj", "DLLStuff/"]
COPY ["DLLStuff/ucrtbased.dll", "DLLStuff/"]
COPY ["DLLStuff/vcruntime140d.dll", "DLLStuff/"]

FROM base AS publish
RUN dotnet restore "DLLStuff/DLLStuff.csproj" --runtime win-x86
COPY . .
WORKDIR "/src/DLLStuff"
RUN dotnet publish "DLLStuff.csproj" -c Release -o /app/publish --runtime win-x86

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DLLStuff.dll"]