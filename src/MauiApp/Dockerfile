FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN dotnet workload install maui-android --skip-manifest-update
WORKDIR /src
COPY src/MauiApp/MauiApp.csproj src/MauiApp/
RUN dotnet restore src/MauiApp/MauiApp.csproj
COPY src/MauiApp/ src/MauiApp/
RUN dotnet publish src/MauiApp/MauiApp.csproj -f net10.0-android -c Release -o /artifacts

FROM scratch
COPY --from=build /artifacts /app
