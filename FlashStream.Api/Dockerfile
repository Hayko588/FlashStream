FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FlashStream.Infrastructure/FlashStream.Infrastructure.csproj FlashStream.Infrastructure/
COPY FlashStream.Domain/FlashStream.Domain.csproj FlashStream.Domain/
COPY FlashStream.Application/FlashStream.Application.csproj FlashStream.Application/
COPY FlashStream.Api/FlashStream.Api.csproj FlashStream.Api/
RUN dotnet restore FlashStream.Api/FlashStream.Api.csproj
COPY . .
WORKDIR /src/FlashStream.Api
RUN dotnet publish FlashStream.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlashStream.Api.dll"]
