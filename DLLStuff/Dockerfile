#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

#FROM mcr.microsoft.com/dotnet/core/runtime:3.1-nanoserver-1809 AS base
FROM mcr.microsoft.com/windows/servercore:ltsc2019 AS base
# Install Chocolatey
RUN powershell -ExecutionPolicy unrestricted -Command iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))
WORKDIR /app

FROM base as withcore
RUN choco install dotnetcore-sdk --confirm
WORKDIR /src 
COPY ["DLLStuff/DLLStuff.csproj", "DLLStuff/"]
RUN dotnet restore "DLLStuff/DLLStuff.csproj" --runtime win-x86
COPY . .

#FROM withcore AS build
#WORKDIR "/src/DLLStuff"
#RUN dotnet build "DLLStuff.csproj" -c Release -o /app/build

#FROM build AS publish
FROM withcore AS publish
WORKDIR "/src/DLLStuff"
RUN dotnet publish "DLLStuff.csproj" -c Release -o /app/publish --runtime win-x86

#FROM publish as final
FROM withcore AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DLLStuff.dll"]