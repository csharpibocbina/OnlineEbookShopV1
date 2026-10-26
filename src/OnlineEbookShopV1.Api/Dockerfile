FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OnlineEbookShopV1.Api/OnlineEbookShopV1.Api.csproj src/OnlineEbookShopV1.Api/
RUN dotnet restore src/OnlineEbookShopV1.Api/OnlineEbookShopV1.Api.csproj
COPY src/OnlineEbookShopV1.Api/ src/OnlineEbookShopV1.Api/
RUN dotnet publish src/OnlineEbookShopV1.Api/OnlineEbookShopV1.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OnlineEbookShopV1.Api.dll"]
