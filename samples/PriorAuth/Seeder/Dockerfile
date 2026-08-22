FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "samples/PriorAuth/Seeder/Kaleido.Samples.PriorAuth.Seeder.csproj"
RUN dotnet publish "samples/PriorAuth/Seeder/Kaleido.Samples.PriorAuth.Seeder.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kaleido.Samples.PriorAuth.Seeder.dll"]
