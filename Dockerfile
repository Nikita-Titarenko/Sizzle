FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

COPY Sizzle.slnx ./
COPY Sizzle.Domain/Sizzle.Domain.csproj Sizzle.Domain/
COPY Sizzle.Application/Sizzle.Application.csproj Sizzle.Application/
COPY Sizzle.Infrastructure/Sizzle.Infrastructure.csproj Sizzle.Infrastructure/
COPY Sizzle.WebApi/Sizzle.WebApi.csproj Sizzle.WebApi/

RUN dotnet restore Sizzle.WebApi/Sizzle.WebApi.csproj

COPY . .

WORKDIR /src/Sizzle.WebApi

EXPOSE 8080

CMD ["dotnet", "watch", "run", "--project", "Sizzle.WebApi.csproj", "--urls", "http://0.0.0.0:8080"]
