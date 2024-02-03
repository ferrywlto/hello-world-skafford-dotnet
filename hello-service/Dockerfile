FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS builder
WORKDIR /app
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=builder /app .
ENV ASPNETCORE_HTTP_PORTS=5001
EXPOSE 5001
ENTRYPOINT ["dotnet", "hello-service.dll"]
