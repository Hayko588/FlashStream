FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FlashStream.Contracts/FlashStream.Contracts.csproj FlashStream.Contracts/
COPY FlashStream.Inventory/FlashStream.Inventory.csproj FlashStream.Inventory/
RUN dotnet restore FlashStream.Inventory/FlashStream.Inventory.csproj
COPY FlashStream.Contracts/ FlashStream.Contracts/
COPY FlashStream.Inventory/ FlashStream.Inventory/
WORKDIR /src/FlashStream.Inventory
RUN dotnet publish FlashStream.Inventory.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlashStream.Inventory.dll"]
