FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OnlineEbookShopV1.Mvc/OnlineEbookShopV1.Mvc.csproj src/OnlineEbookShopV1.Mvc/
RUN dotnet restore src/OnlineEbookShopV1.Mvc/OnlineEbookShopV1.Mvc.csproj
COPY src/OnlineEbookShopV1.Mvc/ src/OnlineEbookShopV1.Mvc/
RUN dotnet publish src/OnlineEbookShopV1.Mvc/OnlineEbookShopV1.Mvc.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OnlineEbookShopV1.Mvc.dll"]
