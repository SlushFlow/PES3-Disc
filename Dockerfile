FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY services/PES3.BugReports.Api/PES3.BugReports.Api.csproj services/PES3.BugReports.Api/
RUN dotnet restore services/PES3.BugReports.Api/PES3.BugReports.Api.csproj
COPY services/PES3.BugReports.Api/ services/PES3.BugReports.Api/
RUN dotnet publish services/PES3.BugReports.Api/PES3.BugReports.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV DATABASE_PATH=/data/reports.db
COPY --from=build /app/publish .
EXPOSE 10000
ENTRYPOINT ["dotnet", "PES3.BugReports.Api.dll"]
