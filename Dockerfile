# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first to leverage layer caching for restore
COPY CommendBod.Model/CommendBod.Model.csproj CommendBod.Model/
COPY CommendBot.Data/CommendBot.Data.csproj CommendBot.Data/
COPY CommendBot/CommendBot.csproj              CommendBot/

RUN dotnet restore CommendBot/CommendBot.csproj

# Copy the rest of the source code
COPY . .

RUN dotnet publish CommendBot/CommendBot.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# The bot is a long-running console process
ENTRYPOINT ["dotnet", "CommendBot.dll"]
