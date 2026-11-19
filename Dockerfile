FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4 AS build
WORKDIR /src
COPY . .
# DataBuilder は net8.0 と net10.0 を対象にしているため、実行イメージに合わせて net10.0 を指定する
RUN dotnet publish src/JaAddress.DataBuilder/JaAddress.DataBuilder.csproj \
    -c Release -f net10.0 -o /app/publish --no-self-contained
# chiseled イメージには shell がないため、出力先ディレクトリはビルドステージで作成する
RUN mkdir /data

# shell・パッケージマネージャを含まず、非 root ユーザー（app）で実行される chiseled イメージを使用する
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled@sha256:1b234cef150624fe8cc7d027e8d742c0f03a16aef2484dc469eb8c872167255e
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build --chown=$APP_UID:$APP_UID /data /data

VOLUME /data
ENTRYPOINT ["dotnet", "JaAddress.DataBuilder.dll", "--output", "/data"]
