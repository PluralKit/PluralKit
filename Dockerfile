FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS build

WORKDIR /app
COPY . /app
RUN dotnet publish -c Release -o out -f netcoreapp3.1

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine
WORKDIR /app
RUN dotnet tool install --global dotnet-trace && dotnet tool install --global dotnet-dump && dotnet tool install --global dotnet-counters
COPY --from=build /app/PluralKit.*/bin/Release/netcoreapp3.1 ./

ENTRYPOINT ["dotnet"]
CMD ["PluralKit.Bot.dll"]

