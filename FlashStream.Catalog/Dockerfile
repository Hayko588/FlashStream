FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FlashStream.Contracts/FlashStream.Contracts.csproj FlashStream.Contracts/
COPY FlashStream.Catalog/FlashStream.Catalog.csproj FlashStream.Catalog/
RUN dotnet restore FlashStream.Catalog/FlashStream.Catalog.csproj
COPY FlashStream.Contracts/ FlashStream.Contracts/
COPY FlashStream.Catalog/ FlashStream.Catalog/
WORKDIR /src/FlashStream.Catalog
RUN dotnet publish FlashStream.Catalog.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlashStream.Catalog.dll"]
