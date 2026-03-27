# Vertical Slice Migration Guide

Bu dokuman, projedeki yeni mimari standardi anlatir:

- Servis sinirlari korunur (`Auth`, `Product`, `Catalog`, `Gateway`).
- Her servis icinde feature-based (vertical slice / union style) duzen kullanilir.
- API katmani ince kalir, use-case mantigi feature handler dosyalarinda toplanir.

## 1) Neden Bu Yapiya Gectik?

Eski modelde bir use-case birden fazla katmana dagiliyordu:

- controller ayri yerde
- command/handler ayri yerde
- mapping ayri yerde
- validation ayri yerde

Bu daginiklik, ekip buyudukce su sorunlari olusturuyordu:

- bir ozelligi takip etmek zorlasiyor
- onboarding suresi uzuyor
- degisiklikler daha fazla dosyaya yayiliyor

Yeni modelde bir use-case'in dosyalari birlikte durur.
Bu sayede okunabilirlik ve bakim kolayligi artar.

## 2) Mevcut Standart (Bu Repo Icin)

### Product

- `src/Services/Product/Product.Api/Features/Products/Create`
- `src/Services/Product/Product.Api/Features/Products/Update`
- `src/Services/Product/Product.Api/Features/Products/List`
- `src/Services/Product/Product.Api/Features/Products/Shared`

### Catalog

- `src/Services/Catalog/Catalog.Api/Features/Catalog/Commands/SyncProductCreated`
- `src/Services/Catalog/Catalog.Api/Features/Catalog/Commands/SyncProductUpdated`
- `src/Services/Catalog/Catalog.Api/Features/Catalog/Queries/GetCatalogItems`
- `src/Services/Catalog/Catalog.Api/Features/Catalog/Queries/GetCatalogSagas`
- `src/Services/Catalog/Catalog.Api/Features/Catalog/Sync`

### Auth

- `src/Services/Auth/Auth.Api/Features/Auth/Register`
- `src/Services/Auth/Auth.Api/Features/Auth/Login`
- `src/Services/Auth/Auth.Api/Features/Auth/Refresh`

## 3) Yeni Feature Acma Kurali

Her yeni ozellik icin minimum set:

1. `Command` veya `Query`
2. `Handler`
3. `Validator`
4. Gerekirse `Result/Dto`
5. Gerekirse `Shared` altinda mapping/helper

Not:
- Validation Handler icinde cagrilabilir (su anki style).
- Controller sadece request->command map + response map yapar.

## 4) Ornek: Yeni Komut Feature'i

Ornek dizin:

`src/Services/Product/Product.Api/Features/Products/Delete`

Olusturulacak dosyalar:

- `DeleteProductCommand.cs`
- `DeleteProductCommandValidator.cs`
- `DeleteProductCommandHandler.cs`
- (opsiyonel) `DeleteProductResult.cs`

## 5) Handler Yazim Kurallari

- Tek bir use-case'e odaklan.
- Sadece gerekli bagimliliklari inject et.
- Validation hatalarini net mesajlarla don.
- Side effect varsa (cache/event/log) handler icinde tek akis olarak tut.
- Büyük handler olursa alt private methodlara bol.

## 6) Controller Yazim Kurallari

- Controller ince olmalidir.
- Is kurali yazilmaz.
- Endpoint basina ortalama 5-20 satir hedeflenir.
- Exception->HTTP map kismi controller seviyesinde kalabilir.

## 7) Repository ve Abstraction Kurali

- "Her seye interface" yazma.
- Repository sadece data access gercekten soyutlanacaksa kullan.
- Tek implementasyonlu ve deger katmayan abstraction'lari zamanla kaldir.
- Shared contract ve gercek cross-cutting konular `Application`/`BuildingBlocks` katmaninda tutulur.

## 8) Event ve Saga Kurali

- Event consume eden use-case'ler de feature olarak modellenir.
- Ornek:
  - `SyncProductCreatedCommandHandler`
  - `SyncProductUpdatedCommandHandler`
- Saga state update + compensation ayni feature akisinda takip edilir.

## 9) Naming Convention

- Query: `GetXxxQuery`, `ListXxxQuery`
- Command: `CreateXxxCommand`, `UpdateXxxCommand`, `SyncXxxCommand`
- Validator: `<CommandOrQuery>NameValidator`
- Handler: `<CommandOrQuery>NameHandler`
- Feature klasoru: use-case bazli olmalidir.

## 10) Pull Request Checklist

PR acmadan once:

- [ ] Yeni endpoint ince mi?
- [ ] Feature dosyalari tek klasorde mi?
- [ ] Validator eklendi mi?
- [ ] Logging ve error mesajlari net mi?
- [ ] Build basarili mi?
- [ ] Gereksiz abstraction eklendi mi? (eklenmemeli)
- [ ] Dokuman gerekiyorsa guncellendi mi?

## 11) Kademeli Iyilestirme Yol Haritasi

Su adimlar sonraki iterasyonlarda uygulanabilir:

1. Tekrarlanan request logging middleware kodunu ortak extension'a almak.
2. Central log forwarder provider kopyalarini tek bir shared pakete tasimak.
3. RabbitMQ `BasicGet` polling yerine event-driven consume modeline gecmek.
4. Controller-level exception map'lerini ortak bir filter/middleware ile sadeleştirmek.

Bu yol haritasi, mevcut sistemi bozmadan kaliteyi adim adim artirmak icin tasarlandi.
