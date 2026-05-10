FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["apps/api/src/PassItOn.Api/PassItOn.Api.csproj", "apps/api/src/PassItOn.Api/"]
RUN dotnet restore "apps/api/src/PassItOn.Api/PassItOn.Api.csproj"

COPY apps/api/src/PassItOn.Api/ apps/api/src/PassItOn.Api/
WORKDIR /src/apps/api/src/PassItOn.Api
RUN dotnet publish "PassItOn.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5200
EXPOSE 5200

ENTRYPOINT ["dotnet", "PassItOn.Api.dll"]
