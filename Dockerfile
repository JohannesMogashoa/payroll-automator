FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY src/PayrollAutomator/PayrollAutomator.csproj src/PayrollAutomator/
RUN dotnet restore src/PayrollAutomator/PayrollAutomator.csproj

COPY . .
RUN dotnet publish src/PayrollAutomator/PayrollAutomator.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV Payroll__DataRoot=/data

RUN mkdir -p /data && chown -R $APP_UID:$APP_UID /data
COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "PayrollAutomator.dll"]
