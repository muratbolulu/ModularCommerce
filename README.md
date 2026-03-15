# ModularCommerce

Bu repo, "Backend Developer - 3. Asama Task" dokumanina uygun olarak Onion + mikroservis mimarisi ile tasarlanmistir.

## Mimari

- `Gateway.Api`: YARP tabanli API Gateway, global rate limiting.
- `Auth.Api`: Microsoft Identity + JWT + Refresh Token.
- `Product.Api`: Onion architecture + CQRS (MediatR) + Redis cache + cache invalidation.
- `Log.Api`: Merkezi structured logging endpoint.
- `Shared.Contracts`: Mikroservisler arasi paylasilan DTO/event sozlesmeleri.

Product servisi katmanlari:

- `Product.Domain`: entity ve domain kurallari.
- `Product.Application`: CQRS command/query ve abstraction katmani.
- `Product.Infrastructure`: EF Core, repository, Redis cache, event publisher.
- `Product.Api`: endpoint ve authorization katmani.

## Gereksinim Karsilama Ozeti

- Onion mimarisi: Product servisinde uygulandi.
- CQRS: `CreateProductCommand`, `UpdateProductCommand`, `GetProductsQuery`.
- JWT + Refresh Token: Auth servisi.
- API Gateway + Rate Limiting: Gateway servisinde global fixed-window policy.
- Redis cache + invalidation: Product listeleme cache'lenir, create/update sonrasinda temizlenir.
- Structured logging: Log servisinde seviye bazli (`INFO/WARNING/ERROR/CRITICAL`) loglama.
- 12-factor uyumu:
  - config degerleri environment variable ile override edilebilir,
  - stateless API processleri,
  - logs stdout uzerinden merkezi toplanmaya uygun.

## Calistirma (Docker)

```bash
docker compose up --build
```

Servisler:

- Gateway: `http://localhost:5000`
- Auth: `http://localhost:5001`
- Product: `http://localhost:5002`
- Log: `http://localhost:5003`

## Calistirma (Lokal, dotnet run)

1. SQL Server ve Redis ayaga kaldirin.
2. Sirayla servisleri calistirin:

```bash
dotnet run --project src/Services/Auth/Auth.Api
dotnet run --project src/Services/Product/Product.Api
dotnet run --project src/Services/Log/Log.Api
dotnet run --project src/Gateway/Gateway.Api
```

Gateway varsayilan local adresler:
- Auth: `http://localhost:5297`
- Product: `http://localhost:5124`
- Log: `http://localhost:5092`

## Ornek Akis

1. Register:
```http
POST /auth/register
{
  "email": "admin@modularcommerce.local",
  "password": "Password1"
}
```

2. Login:
```http
POST /auth/login
{
  "email": "admin@modularcommerce.local",
  "password": "Password1"
}
```

3. Product ekle (Bearer token ile):
```http
POST /products
{
  "name": "Keyboard",
  "price": 1200,
  "stock": 15
}
```

4. Product listele:
```http
GET /products
```

## Gelistirme Notlari

- Event publish katmani `IEventPublisher` abstraction'i ile ayrildi; RabbitMQ/Kafka adaptoru eklemek icin `Product.Infrastructure` altina yeni implementasyon yeterlidir.
- Authorization role/policy tabanli olacak sekilde tasarlandi (`ProductWriterPolicy`).
- CI/CD ve SAGA, dokumandaki "ekstra degerlendirme" kismina uygun olarak sonraki iterasyonda genisletilebilir.
