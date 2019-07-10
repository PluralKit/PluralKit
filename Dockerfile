FROM mcr.microsoft.com/dotnet/core/sdk:2.2-alpine

WORKDIR /app
COPY PluralKit.API /app/PluralKit.API
COPY PluralKit.Bot /app/PluralKit.Bot
COPY PluralKit.Core /app/PluralKit.Core
COPY PluralKit.Web /app/PluralKit.Web
COPY PluralKit.sln /app
RUN dotnet build
