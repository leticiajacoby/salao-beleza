FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SalaoBeleza.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish SalaoBeleza.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# O Render informa a porta pela variavel PORT.
ENV ASPNETCORE_ENVIRONMENT=Production
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} dotnet SalaoBeleza.dll"]
