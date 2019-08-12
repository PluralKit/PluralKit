FROM mcr.microsoft.com/dotnet/core/sdk:2.2-alpine

WORKDIR /app

# Copy all solution files to container, and run restore
COPY PluralKit.API/PluralKit.API.csproj /app/PluralKit.API/
COPY PluralKit.Bot/PluralKit.Bot.csproj /app/PluralKit.Bot/
COPY PluralKit.Core/PluralKit.Core.csproj /app/PluralKit.Core/
COPY PluralKit.Web/PluralKit.Web.csproj /app/PluralKit.Web/
COPY PluralKit.sln /app
RUN dotnet restore 

# Copy actual source code to container and build
COPY PluralKit.API /app/PluralKit.API
COPY PluralKit.Bot /app/PluralKit.Bot
COPY PluralKit.Core /app/PluralKit.Core
COPY PluralKit.Web /app/PluralKit.Web
RUN dotnet build

ENTRYPOINT dotnet run