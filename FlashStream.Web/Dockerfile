FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FlashStream.Web/FlashStream.Web.csproj FlashStream.Web/
RUN dotnet restore FlashStream.Web/FlashStream.Web.csproj
COPY FlashStream.Web/ FlashStream.Web/
WORKDIR /src/FlashStream.Web
RUN dotnet publish FlashStream.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlashStream.Web.dll"]
