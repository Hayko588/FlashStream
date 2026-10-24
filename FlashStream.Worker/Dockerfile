FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FlashStream.Infrastructure/FlashStream.Infrastructure.csproj FlashStream.Infrastructure/
COPY FlashStream.Domain/FlashStream.Domain.csproj FlashStream.Domain/
COPY FlashStream.Application/FlashStream.Application.csproj FlashStream.Application/
COPY FlashStream.Worker/FlashStream.Worker.csproj FlashStream.Worker/
RUN dotnet restore FlashStream.Worker/FlashStream.Worker.csproj
COPY . .
WORKDIR /src/FlashStream.Worker
RUN dotnet publish FlashStream.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FlashStream.Worker.dll"]
