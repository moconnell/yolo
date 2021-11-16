FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["YoloKonsole/YoloKonsole.csproj", "YoloKonsole/"]
RUN dotnet restore "YoloKonsole/YoloKonsole.csproj"
COPY . .
WORKDIR "/src/YoloKonsole"
RUN dotnet build "YoloKonsole.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "YoloKonsole.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "YoloKonsole.dll"]
