FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/PES3-Disc.BugReports/PES3-Disc.BugReports.csproj src/PES3-Disc.BugReports/
COPY services/PES3.BugReports.Api/PES3.BugReports.Api.csproj services/PES3.BugReports.Api/
RUN dotnet restore services/PES3.BugReports.Api/PES3.BugReports.Api.csproj
COPY src/PES3-Disc.BugReports/ src/PES3-Disc.BugReports/
COPY services/PES3.BugReports.Api/ services/PES3.BugReports.Api/
RUN dotnet publish services/PES3.BugReports.Api/PES3.BugReports.Api.csproj -c Release -o /app/publish -p:LangVersion=12

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV DATABASE_PATH=/data/reports.db
COPY --from=build /app/publish .
EXPOSE 10000
ENTRYPOINT ["dotnet", "PES3.BugReports.Api.dll"]
