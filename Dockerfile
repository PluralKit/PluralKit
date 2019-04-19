FROM mcr.microsoft.com/dotnet/core/sdk:2.2-alpine

WORKDIR /app
COPY PluralKit/ PluralKit.csproj /app/
RUN dotnet build
ENTRYPOINT ["dotnet", "run"]