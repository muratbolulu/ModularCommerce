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

## Calisma Mantigi

Sistem istemciden gelen tum istekleri once `Gateway.Api` uzerinden alir. Gateway, route kurallarina gore istegi ilgili mikroservise yonlendirir ve global rate limit ile asiri istekleri sinirlar.

Auth akisinda:
- `POST /auth/register` ile kullanici Identity tablosuna kaydedilir.
- `POST /auth/login` ile kullanici dogrulanir; access token (JWT) ve refresh token uretilir.
- `POST /auth/refresh` ile gecerli refresh token karsiliginda yeni token cifti verilir.

Product akisinda (CQRS):
- Yazma islemleri (`POST /products`, `PUT /products/{id}`) command handler'lara gider.
- Okuma islemi (`GET /products`) query handler ile ayrik olarak calisir.
- Yazma sonrasi `ProductCreatedEvent` / `ProductUpdatedEvent` yayinlanir.

Cache mantigi:
- `GET /products` sonucunda liste Redis'e yazilir.
- Urun ekleme/guncelleme oldugunda ilgili cache anahtari silinir (cache invalidation).
- Sonraki listeleme istegi taze veriyi DB'den alip tekrar cache'ler.

Yetkilendirme:
- Product yazma endpointleri JWT ve role/policy kontrolu ister (`ProductWriterPolicy`).
- Product listeleme endpointi anonim erisime aciktir.

Loglama:
- Servisler loglari JSON/structured formatta uretmeye uygundur.
- `Log.Api`, seviye bazli (`INFO`, `WARNING`, `ERROR`, `CRITICAL`) merkezi log toplama endpointi sunar.

## API Gateway Detayli Calisma Prensibi

`Gateway.Api`, YARP (`Yarp.ReverseProxy`) uzerinde calisan bir ters proxy katmanidir. Istemci, mikroservislerin gercek adreslerini bilmeden sadece Gateway'e istek atar; Gateway de path kurallarina gore uygun servise yonlendirir.

### 1) Route -> Cluster -> Destination modeli

Gateway konfigrasyonu `src/Gateway/Gateway.Api/appsettings.json` icinde `ReverseProxy` altindadir:

- `Route`: Dis dunyadan gelen path'i esler.
- `Cluster`: O path'in yonlendirilecegi mantiksal servis grubudur.
- `Destination`: Cluster icindeki gercek hedef URL'dir.

Bu projede temel route eslesmeleri:

- `/auth` ve `/auth/{**catch-all}` -> `auth-cluster`
- `/products` ve `/products/{**catch-all}` -> `product-cluster`
- `/logs` ve `/logs/{**catch-all}` -> `log-cluster`

Lokal debug modunda destination adresleri:

- `auth-cluster` -> `http://localhost:5297/`
- `product-cluster` -> `http://localhost:5124/`
- `log-cluster` -> `http://localhost:5092/`

Docker modunda bu destination'lar ortam degiskenleriyle container isimlerine override edilir:

- `auth-api:8080`
- `product-api:8080`
- `log-api:8080`

### 2) Request pipeline sirasi

Gateway tarafinda istek su sirada islenir:

1. Istek Gateway portuna gelir (lokal: `5228`, docker: `5000`).
2. Global rate limiter calisir.
3. YARP route matching yapar ve hedef cluster'i secilir.
4. Request transform calisir (`X-Gateway: modular-commerce-gateway` header'i eklenir).
5. Istek ilgili mikroservise proxy edilir.
6. Mikroservis cevabi oldugu gibi istemciye geri doner.

### 3) Rate limiting davranisi

Gateway'de global fixed-window limiter vardir:

- pencere: `10` saniye
- limit: `20` istek / IP
- kuyruk: `2` istek
- limit asiminda donen durum kodu: `429 Too Many Requests`

Bu sayede tum endpointler merkezi olarak korunur; mikroservislerin her birinde ayri rate limit yazmaya gerek kalmaz.

### 4) Neden API Gateway kullaniliyor?

Bu yapi su avantajlari saglar:

- tek giris noktasi (single entrypoint),
- merkezi trafik yonetimi (rate limit, header policy),
- istemciyi servis adreslerinden bagimsizlastirma,
- servisleri ayri ayri tasirken istemciyi bozmama (sadece gateway config degisir),
- izlenebilirligi artirma (talepler tek noktadan gecer).

### 5) Hata ayiklama ve dogrulama

Gateway calisma kontrolu:

- `GET /health` ile ayakta oldugu dogrulanir.
- `GET /openapi/v1.json` ile uygulamanin acildigi gorulur.

Proxy dogrulama:

- `POST /auth/login` istegi Gateway uzerinden atildiginda Auth servisine ulasmali.
- `GET /products` istegi Gateway uzerinden Product servisine ulasmali.
- `POST /logs` istegi Gateway uzerinden Log servisine ulasmali.

Eger Gateway ayakta ama proxy basarisizsa ilk bakilacak noktalar:

- hedef mikroservislerin calisip calismadigi,
- `ReverseProxy` destination URL'lerinin dogrulugu,
- lokal/docker port farki (`5228` vs `5000`),
- rate limit nedeniyle `429` alinip alinmadigi.

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
