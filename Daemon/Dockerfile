FROM mcr.microsoft.com/dotnet/sdk:6.0 AS base
ENV TZ=America/Chicago
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
COPY . /src
WORKDIR /src
RUN ls
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
CMD [ "dotnet", "DiscordChatGPT.Daemon.dll" ]