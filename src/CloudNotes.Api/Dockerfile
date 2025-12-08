# ===== Build Stage =====
# Используем SDK образ для сборки приложения
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем файл проекта и восстанавливаем зависимости
COPY ["CloudNotes.Api.csproj", "./"]
RUN dotnet restore "CloudNotes.Api.csproj"

# Копируем остальные файлы и собираем приложение
COPY . .
RUN dotnet build "CloudNotes.Api.csproj" -c Release -o /app/build

# ===== Publish Stage =====
# Публикуем приложение для production
FROM build AS publish
RUN dotnet publish "CloudNotes.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ===== Runtime Stage =====
# Используем runtime образ для запуска приложения
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Устанавливаем curl для health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Копируем опубликованные файлы из publish stage
COPY --from=publish /app/publish .

# Устанавливаем переменную окружения для порта
ENV ASPNETCORE_URLS=http://+:8080

# Открываем порт 8080
EXPOSE 8080

# Точка входа для запуска приложения
ENTRYPOINT ["dotnet", "CloudNotes.Api.dll"]

