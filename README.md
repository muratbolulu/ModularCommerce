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
- Yazma sonrasi `ProductCreatedEvent` / `ProductUpdatedEvent` RabbitMQ exchange uzerinden yayinlanir.
- Uygulama acilisinda RabbitMQ exchange/queue/binding topolojisi otomatik olusturulur.

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

### 2) Request pipeline sirasi

Gateway tarafinda istek su sirada islenir:

1. Istek Gateway portuna gelir (lokal: `5228`).
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
- servislerin lokal portlarda ayakta oldugu,
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

## Calistirma (Lokal, dotnet run)

1. SQL Server ve Redis ayaga kaldirin.
2. RabbitMQ ayaga kaldirin (varsayilan: `localhost:5672`, `guest/guest`).
3. Sirayla servisleri calistirin:

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

## Docker Kullandigimiz Yerler

Bu projede uygulama servisleri (`Auth.Api`, `Product.Api`, `Log.Api`, `Gateway.Api`) lokal `dotnet run` ile calistirilir.
Docker, destek servislerini hizli ve izole sekilde ayaga kaldirmak icin kullanilir:

- `Redis` (cache): `localhost:6379`
- `RabbitMQ` (event broker): `localhost:5672`
- `RabbitMQ Management UI`: `http://localhost:15672` (`guest/guest`)
- `Elasticsearch` (log index): `http://localhost:9200`
- `Kibana` (log analiz): `http://localhost:5601`

Ornek Docker komutlari:

```bash
docker run -d --name redis -p 6379:6379 redis:7
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
docker run -d --name elasticsearch -p 9200:9200 -e "discovery.type=single-node" -e "xpack.security.enabled=false" docker.elastic.co/elasticsearch/elasticsearch:8.17.0
docker run -d --name kibana -p 5601:5601 -e "ELASTICSEARCH_HOSTS=http://host.docker.internal:9200" docker.elastic.co/kibana/kibana:8.17.0
```

Not:
- Kibana Linux container'inda host Elasticsearch'e baglanirken `host.docker.internal` kullanilir.
- Bu portlar `appsettings.json` degerleriyle uyumludur (`REDIS_CONNECTION`, `RABBITMQ_*`, `ELASTICSEARCH_URL`).

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
- RabbitMQ ayarlari `Product.Api/appsettings.json` icinde `RABBITMQ_*` anahtarlariyla yonetilir.
- Varsayilan routing key ve queue eslesmeleri:
  - `product.created` -> `product.created.queue`
  - `product.updated` -> `product.updated.queue`
- Authorization role/policy tabanli olacak sekilde tasarlandi (`ProductWriterPolicy`).
- CI/CD ve SAGA, dokumandaki "ekstra degerlendirme" kismina uygun olarak sonraki iterasyonda genisletilebilir.

## Kullanilan Patternler ve Nedenleri

Bu bolumde projede secilen temel yaklasimlarin "neden"i ozetlenir.

### 1) Onion Architecture

- `Domain`, `Application`, `Infrastructure`, `Api` katmanlari birbirinden ayridir.
- Neden: Is kurallarini teknik bagimliliklardan ayirmak, test edilebilirligi ve degistirilebilirligi artirmak.

### 2) CQRS (Command/Query Responsibility Segregation)

- Yazma (`CreateProduct`, `UpdateProduct`) ve okuma (`GetProducts`) akislarinin ayri handler'lari vardir.
- Neden: Okuma/yazma gereksinimleri farkli oldugu icin performans ve bakim avantajı saglar; cache stratejisi query tarafinda netlesir.

### 3) Repository + Abstraction

- `IProductRepository`, `ICacheService`, `IEventPublisher` gibi arayuzler kullanilir.
- Neden: Uygulama katmani altyapi detaylarini bilmez; SOLID (ozellikle DIP) uyumu saglanir.

### 4) Event-Driven yaklasim (RabbitMQ)

- Product degisiklikleri olay olarak publish edilir (`product.created`, `product.updated`).
- Neden: Mikroservisler arasinda gevsek baglilik, asenkron isleme, olceklenebilir entegrasyon.

### 5) RabbitMQ'da Neden Binding Kullaniyoruz?

- Exchange tek basina mesaji kuyruga gondermez; hangi mesajin hangi kuyruga gidecegi `binding` ile tanimlanir.
- Bu projede:
  - `product.created` routing key'i -> `product.created.queue`
  - `product.updated` routing key'i -> `product.updated.queue`
- Neden:
  - Mesajlari tipine gore ayristirip dogru tuketiciye yonlendirmek
  - Yeni event tipleri eklendiginde mevcut akis bozulmadan yeni queue/binding tanimlayabilmek
  - Producer (Product API) ile consumer'lari bagimsiz tutmak

### 6) API Gateway + Rate Limiting

- YARP Gateway ile tek giris noktasi, global fixed-window rate limit uygulanir.
- Neden: Merkezi trafik yonetimi, istemciyi servis adreslerinden bagimsizlastirma ve sistemin korunmasi.

### 7) Structured Logging + Merkezi Toplama

- Loglar JSON formatinda uretilir ve merkezi `Log.Api` uzerinden toplanir (Elastic/Seq sinkleri desteklenir).
- Neden: Aranabilir, filtrelenebilir ve izlenebilir operasyonel gorunurluk saglamak.
