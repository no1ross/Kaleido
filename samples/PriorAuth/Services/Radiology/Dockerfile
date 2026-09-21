FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "samples/PriorAuth/Radiology/Kaleido.Samples.PriorAuth.Radiology.csproj"
RUN dotnet publish "samples/PriorAuth/Radiology/Kaleido.Samples.PriorAuth.Radiology.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data
ENTRYPOINT ["dotnet", "Kaleido.Samples.PriorAuth.Radiology.dll"]
