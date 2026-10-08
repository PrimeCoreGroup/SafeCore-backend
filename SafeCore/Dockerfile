# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia el csproj y restaura
COPY ["SafeCore/SafeCore.csproj", "SafeCore/"]
RUN dotnet restore "SafeCore/SafeCore.csproj"

# Copia todo el código
COPY . .
WORKDIR "/src/SafeCore"
RUN dotnet build "SafeCore.csproj" -c Release -o /app/build

# Publica
FROM build AS publish
RUN dotnet publish "SafeCore.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Imagen final
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "SafeCore.dll"]