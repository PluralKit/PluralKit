FROM mcr.microsoft.com/dotnet/core/sdk:2.2-alpine AS build

WORKDIR /app
COPY . /app
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:2.2-alpine
WORKDIR /app
COPY --from=build /app/PluralKit.*/out ./

ENTRYPOINT ["dotnet"]
CMD ["PluralKit.Bot.dll"]

