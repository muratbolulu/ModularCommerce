# Product Olusturma Akisi (Adim Adim)

Bu dokuman, sistemi ilk kez kuran biri icin yazildi.
Amac: `bir urun olusturuldugunda` istegin hangi servislere girdigini, nerelerden ciktigini, hangi veritabanina ne zaman yazildigini net gostermek.

## 1) Buyuk Resim

`POST /products` istegi ilk olarak **Gateway**'e gelir, sonra **Product.Api**'ye gider.
Product DB'ye yazildiktan sonra event RabbitMQ'ya yayinlanir.
Ardindan **Catalog.Api** bu eventi tuketir ve kendi DB'sine yazar.

Kisa ozet:

1. Client -> Gateway
2. Gateway -> Product.Api
3. Product.Api -> ProductDb (write)
4. Product.Api -> RabbitMQ (`product.created`)
5. RabbitMQ -> Catalog.Api consumer
6. Catalog.Api -> CatalogDb (write)
7. Catalog saga state guncellenir (`Completed` veya `Failed`)

## 2) Istek Hangi Endpoint'e Gelir?

- Disaridan (Postman/Frontend): `POST http://localhost:5228/products` (Gateway)
- Gateway, route kuraliyla bunu Product servisine proxy eder:
  - hedef: `http://localhost:5124/products`

Yani client sadece gateway'i gorur; Product'in gercek adresini bilmek zorunda degildir.

## 3) Product Tarafinda Neler Olur?

`Product.Api` icinde create endpoint command handler'i calistirir:

1. JWT/policy kontrolu yapilir (`ProductWriterPolicy`)
2. `ProductEntity` olusturulur
3. `ProductDb` icindeki `Products` tablosuna kaydedilir
4. Cache listesi invalid edilir (`products:list:v1`)
5. `ProductCreatedEvent` RabbitMQ exchange'ine publish edilir

Bu noktada **product kaydi kalici olarak ProductDb'ye yazilmistir**.

## 4) RabbitMQ'da Nereye Gider?

Product event'i exchange'e topic routing key ile gider:

- exchange: `modularcommerce.product.events`
- routing key: `product.created`

Bu event, `Catalog.Api` tarafinin dinledigi queue'ya bind oldugu icin Catalog tarafina ulasir.

## 5) Catalog Tarafinda Neler Olur?

`CatalogRabbitMqConsumer` mesaji alir ve orchestrator'a verir:

1. Saga kaydi acilir (`catalog-create-{productId}`)
2. State: `Started`
3. Catalog write yapilir (`CatalogItems` tablosu)
4. State: `CatalogWriteCompleted`
5. State: `Completed`

Yani Catalog servisinde event-driven bir read model olusur.

## 6) Basarili Akisin Giris/Cikis Noktalari

### Giris
- HTTP giris: Gateway (`/products`)
- Message giris: Catalog queue (`product.created`)

### Cikis
- HTTP cikis: Product create response (200 + product id)
- Message cikis: RabbitMQ publish
- DB cikislari:
  - ProductDb: `Products` insert
  - CatalogDb: `CatalogItems` insert/upsert
  - CatalogDb: `CatalogSagas` state kaydi

## 7) Hata Senaryosu (Catalog Yazamazsa)

Bu projede ekstra compensation vardir:

1. Catalog tarafinda yazim hata alirsa saga state `Failed` olur
2. `CatalogWriteFailedEvent` publish edilir (`catalog.write.failed`)
3. Product tarafindaki consumer bu eventi alir
4. Ilgili product **soft-delete** yapilir:
   - `IsDeleted = true`
   - `IsActive = false`
5. Sonraki `GET /products` listesinde bu urun gorunmez

Bu sayede "Catalog'a yazilamadi ama Product'ta kaldi" tutarsizligi azaltilir.

## 8) Bu Akisin Neden Boyle Tasarlandigi

- Mikroservisler bagimsiz kalir (her servis kendi DB'sini yazar)
- Servisler arasi baglilik event ile kurulur (synchronous zincir yok)
- Sistem daha dayanikli olur (RabbitMQ ile asenkron iletim)
- Saga state sayesinde "hangi adimda ne oldu" izlenebilir
- Compensation ile is kurali korunur (Catalog fail ise Product pasiflenir)

## 9) Nereden Kontrol Edebilirim?

- Product listesi: `GET /products`
- Catalog listesi: `GET /catalog/items`
- Saga durumu: `GET /catalog/sagas`
- Loglar:
  - `Log.Api` endpointleri
  - Elasticsearch/Kibana (varsa)

## 10) Tek Cumlelik Mental Model

Product create, once ProductDb'ye yazilir; sonra event ile Catalog'a tasinir; Catalog basarisiz olursa compensation eventi ile Product soft-delete edilir.
