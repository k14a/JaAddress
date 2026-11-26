FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src
COPY . .
# DataBuilder は net8.0 と net10.0 を対象にしているため、実行イメージに合わせて net10.0 を指定する
RUN dotnet publish src/JaAddress.DataBuilder/JaAddress.DataBuilder.csproj \
    -c Release -f net10.0 -o /app/publish --no-self-contained
# chiseled イメージには shell がないため、出力先ディレクトリはビルドステージで作成する
RUN mkdir /data

# shell・パッケージマネージャを含まず、非 root ユーザー（app）で実行される chiseled イメージを使用する
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled@sha256:d7841dcba3234bd757f835cdaf03de1cae42b301e7f19677cd87a7bbc2064b27
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build --chown=$APP_UID:$APP_UID /data /data

VOLUME /data
ENTRYPOINT ["dotnet", "JaAddress.DataBuilder.dll", "--output", "/data"]
