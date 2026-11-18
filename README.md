# CSCI3020 Project 2: MongoDB MAUI System

A .NET 10 starter system containing:

- a native .NET MAUI Android/Windows/iOS/Mac Catalyst app,
- an ASP.NET Core API with CRUD endpoints,
- MongoDB, and
- Docker configuration for MongoDB and the API.

## Run the API and MongoDB

```powershell
docker compose up --build api mongodb
```

Open `http://localhost:8080/openapi/v1.json` for the API description.
The item API is available at `http://localhost:8080/api/items`.

Example request:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:8080/api/items" `
  -ContentType "application/json" `
  -Body '{"name":"Example","description":"Created from PowerShell"}'
```

## Run the MAUI app

MAUI desktop and mobile applications are native applications; Docker cannot run their UI or Android/iOS simulator. Run the app on Windows or Android:

```powershell
dotnet restore MongoMauiSystem.sln
dotnet build src/MauiApp/MauiApp.csproj -f net10.0-windows10.0.19041.0
dotnet build src/MauiApp/MauiApp.csproj -t:Run -f net10.0-android
```

This will actually run the UI
```powershell
dotnet run --project src/MauiApp/MauiApp.csproj -f net10.0-windows10.0.19041.0
```

The MAUI app calls the API. `10.0.2.2:8080` is the Android Emulator alias for the Windows host. Windows and desktop development should change `BaseAddress` in `MainPage.xaml.cs` to `http://localhost:8080`.

To build an Android MAUI package inside Docker:

```powershell
docker compose --profile maui build maui-builder
```

Stop and remove containers and the database volume:

```powershell
docker compose down -v
```