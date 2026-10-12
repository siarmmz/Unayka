FROM node:22-alpine AS styles
WORKDIR /src
RUN npm install -g tailwindcss @tailwindcss/cli
ENV NODE_PATH=/usr/local/lib/node_modules
COPY src/Unayka.Web/Components ./src/Unayka.Web/Components
COPY src/Unayka.Web/wwwroot/styles/globals.css ./src/Unayka.Web/wwwroot/styles/globals.css
RUN tailwindcss -i ./src/Unayka.Web/wwwroot/styles/globals.css -o ./src/Unayka.Web/wwwroot/styles/app.css --minify

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Directory.Build.props ./src/
COPY .editorconfig ./
COPY src/Unayka.Core/Unayka.Core.csproj ./src/Unayka.Core/
COPY src/Unayka.Web/Unayka.Web.csproj ./src/Unayka.Web/
RUN dotnet restore src/Unayka.Web/Unayka.Web.csproj
COPY src/Unayka.Core ./src/Unayka.Core
COPY src/Unayka.Web ./src/Unayka.Web
COPY --from=styles /src/src/Unayka.Web/wwwroot/styles/app.css ./src/Unayka.Web/wwwroot/styles/app.css
RUN dotnet publish src/Unayka.Web/Unayka.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Unayka.Web.dll"]
