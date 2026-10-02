# syntax=docker/dockerfile:1
# Image de GlpiNg.Web, exécutée sans droits root (utilisateur « app », UID 1654, des images .NET).
# À construire depuis la racine du dépôt, sous-modules récupérés (git submodule update --init).

# Compilation sur la plateforme de la machine de build, pour l'architecture cible : pas
# d'émulation pour une image arm64.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
ARG VERSION=0.0.0-dev
WORKDIR /src
COPY . .
RUN dotnet publish src/GlpiNg.Web/GlpiNg.Web.csproj -c Release -a "$TARGETARCH" --self-contained false \
      -p:Version="$VERSION" -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# libldap : bibliothèque native de System.DirectoryServices.Protocols (authentification LDAP).
RUN apt-get update \
 && apt-get install -y --no-install-recommends libldap2 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# /app reste à root, en lecture seule pour l'application. Seuls les deux fichiers qu'elle réécrit
# depuis /config lui appartiennent.
# ponytail: ces deux fichiers vivent dans l'image, pas dans /data : ils repartent de zéro quand le
# conteneur est recréé. Les ranger sous la racine du stockage si ça devient gênant.
RUN echo "[]" > config-history.json \
 && chown "$APP_UID" appsettings.json config-history.json \
 && mkdir /data && chown "$APP_UID" /data

# Tout ce qui est propre à l'installation (appsettings.local.json, clés, paquets, documents,
# plugins) est sous la racine du stockage : un seul volume à sauvegarder.
# « Urls » sans préfixe ASPNETCORE_ : passe devant la valeur d'appsettings.json.
ENV Storage__RootPath=/data \
    Urls=http://+:8080
VOLUME /data
EXPOSE 8080

USER $APP_UID
# glping est aussi la CLI : « docker run <image> user:create jdupont » remplace « serve ».
ENTRYPOINT ["dotnet", "glping.dll"]
CMD ["serve"]
