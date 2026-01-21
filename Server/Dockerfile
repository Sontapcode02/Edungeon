# 1. Build Layer
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy everything
COPY . .

# Restore and Publish
# Note: Use quotes because the folder name contains '#'
WORKDIR "/app/Server_Edungeon_Unity_C#"
RUN dotnet restore
RUN dotnet publish -c Release -o /app/out

# 2. Runtime Layer
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out .

# Expose the WebSocket port (Render will map this internally, but good for documentation)
EXPOSE 7780

# Command to run the application
CMD ["dotnet", "Server_Edungeon_Unity_C#.dll"]
