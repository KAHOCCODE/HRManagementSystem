FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY HRManagementSystem/HRManagementSystem.csproj HRManagementSystem/
RUN dotnet restore HRManagementSystem/HRManagementSystem.csproj
COPY HRManagementSystem/ HRManagementSystem/
RUN dotnet publish HRManagementSystem/HRManagementSystem.csproj -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 TZ=Asia/Ho_Chi_Minh
COPY --chown=app:app --from=build /publish/ ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "HRManagementSystem.dll"]
