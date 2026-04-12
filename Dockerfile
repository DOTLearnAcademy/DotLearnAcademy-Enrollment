FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotLearn.Enrollment/DotLearn.Enrollment.csproj", "DotLearn.Enrollment/"]
RUN dotnet restore "DotLearn.Enrollment/DotLearn.Enrollment.csproj"
COPY . .
WORKDIR "/src/DotLearn.Enrollment"
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DotLearn.Enrollment.dll"]
