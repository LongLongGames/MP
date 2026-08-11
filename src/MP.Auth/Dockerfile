# ---- 编译阶段 ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN apt-get update && apt-get install -y clang zlib1g-dev && rm -rf /var/lib/apt/lists/*

COPY ["src/MP.Auth/MP.Auth.csproj", "src/MP.Auth/"]
RUN dotnet restore "src/MP.Auth/MP.Auth.csproj"

COPY . .
WORKDIR "/src/src/MP.Auth"
RUN dotnet publish "MP.Auth.csproj" -c Release -r linux-x64 -o /app/publish /p:PublishAot=true

# ---- 运行阶段：无 .NET 运行时依赖的极简底包 ----
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["./MP.Auth"]
