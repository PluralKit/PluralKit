FROM mcr.microsoft.com/dotnet/core/sdk:3.1.401
WORKDIR /app

# Restore/fetch dependencies excluding app code to make use of caching
COPY PluralKit.sln nuget.config /app/
COPY PluralKit.API/PluralKit.API.csproj /app/PluralKit.API/
COPY PluralKit.Bot/PluralKit.Bot.csproj /app/PluralKit.Bot/
COPY PluralKit.Core/PluralKit.Core.csproj /app/PluralKit.Core/
COPY PluralKit.Tests/PluralKit.Tests.csproj /app/PluralKit.Tests/
RUN dotnet restore PluralKit.sln

# Copy the rest of the code and build
COPY . /app
RUN dotnet build -c Release -o bin

# Run :)
# Allow overriding CMD from eg. docker-compose to run API layer too
ENTRYPOINT ["dotnet"]
CMD ["bin/PluralKit.Bot.dll"]