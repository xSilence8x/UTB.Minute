# Objednavaci system v menze (UTB Minute)

Semestralni projekt do predmetu **Aplikacni frameworky**. Aplikace slouzi pro spravu minutkovych jidel v menze, objednavani studenty a zpracovani objednavek kuchyni.

Reseni je rozdeleno na Aspire orchestrace, databazovou vrstvu, sdilene kontrakty, Minimal Web API, databazovy manager, testy a dva Blazor Server klienty.

## Clenove tymu a pomer prace

| Jmeno a prijmeni | Role v tymu | Pomer prace |
|:---|:---|:---:|
| **Milan Kedroň** - vedouci | Datovy model & Backend | 1 |
| **Markéta Dalecká** | WebAPI & SSE | 1 |
| **Kamila Petřeková** | Blazor klient & UI | 1 |

Pomer prace `1:1:1` znamena rovnomerny prinos vsech clenu tymu.

## Spusteni projektu

### Pozadavky

- .NET 10 SDK
- Docker Desktop nebo Podman pro beh PostgreSQL a Keycloaku v .NET Aspire
- Visual Studio 2026, JetBrains Rider nebo prikazova radka s .NET SDK

### Postup

```powershell
dotnet restore UTB.Minute.slnx
dotnet run --project .\UTB.Minute.AppHost\UTB.Minute.AppHost.csproj
```

Po spusteni `UTB.Minute.AppHost` se spusti PostgreSQL, Keycloak, Web API, databazovy manager a oba Blazor klienti. V prohlizeci je dostupny .NET Aspire Dashboard, kde jsou videt jednotlive sluzby a odkazy na klientské aplikace.

Databazovy manager vystavuje HTTP command `POST /commands/reset-database`, ktery je v Aspire zaregistrovany jako prikaz **Reset database**.

Pro prime spusteni Web API bez Aspire se pouzije lokalni SQLite databaze `minute-dev.db`.

## Struktura reseni

- `UTB.Minute.AppHost` - Aspire orchestrace PostgreSQL, Keycloaku, Web API, databazoveho manageru a klientskych aplikaci.
- `UTB.Minute.Db` - Entity Framework Core entity, `MinuteDbContext`, stav objednavky a seed/reset databaze.
- `UTB.Minute.Contracts` - sdilene DTO, request modely a konstanty API rout.
- `UTB.Minute.WebApi` - Minimal API, byznys logika, autorizace, prace s objednavkami a Server-Sent Events.
- `UTB.Minute.DbManager` - servis pro reset a naplneni databaze.
- `UTB.Minute.WebApi.Tests` - integracni testy pro jidla, menu, objednavky a zmeny stavu.
- `UTB.Minute.AdminClient` - Blazor Server aplikace pro vedeni menzy a spravu jidel/menu.
- `UTB.Minute.CanteenClient` - Blazor Server rozhrani pro studenty a kuchyni.

## Klicova implementacni rozhodnuti

### 1. Autorizace a Keycloak

Zamestnanecke casti aplikace jsou chranene roli z Keycloaku. Web API pouziva vlastni autentizacni handler, ktery overuje Bearer tokeny proti OpenID metadata a JWKS z Keycloak realmu.

Pouzite role:

- `Manager` - vytvareni a uprava jidel, vytvareni/uprava/mazani polozek menu.
- `Kitchen` - zobrazeni aktivnich objednavek a zmena jejich stavu.

Aspire importuje realm `minute` ze souboru `UTB.Minute.AppHost/Keycloak/minute-realm.json`. Pro vyvoj jsou dostupni testovaci uzivatele `manager` a `kitchen` se stejnymi hesly jako prihlasovaci jmena.

Studenti se neprihlasuji pres Keycloak. Objednavka je anonymni a student po vytvoreni pracuje s cislem objednavky, ktere vidi na verejne objednavkove tabuli.

V lokalnim vyvojovem rezimu je pro testovani podporovana hlavicka `X-Debug-Role`, napriklad `Manager`, `Kitchen` nebo `Manager,Kitchen`.

### 2. Real-time notifikace (SSE)

Real-time aktualizace objednavek jsou resene pomoci Server-Sent Events na endpointu `GET /api/orders/events`. Web API publikuje udalosti pri vytvoreni objednavky a pri zmene jejiho stavu.

Implementace pouziva tridu `OrderEventStream`, ktera spravuje odberatele pomoci kanalu. Blazor klient odebira SSE stream a po prijeti udalosti aktualizuje studentsky i kuchynsky pohled bez rucniho obnoveni stranky.

### 3. Business pravidla

Objednavku lze vytvorit pouze pro existujici polozku menu s dostupnym poctem porci. Pri vytvoreni objednavky se v databazove transakci snizi `AvailablePortions`. Pokud jsou porce vyprodane, API vrati konflikt.

Polozka menu ma concurrency token `Version`. Pri soubeznych zmenach EF Core detekuje konflikt a aplikace tim omezuje riziko preobjednani jidla pri vice paralelnich pozadavcich.

Stavy objednavky jsou omezeny pravidly v `OrderRules`:

- `Preparing` muze prejit na `Ready`, `Cancelled` nebo `Completed`.
- `Ready` muze prejit na `Completed`.
- `Cancelled` muze prejit na `Completed`.
- `Completed` je konecny stav.

## Poznamky k odevzdani

- **Stav:** Projekt je funkcni a pokryva hlavni tok od spravy jidel pres objednani studentem az po zpracovani kuchyni.
- **Testovani:** Integracni testy v `UTB.Minute.WebApi.Tests` overuji jidla, menu, objednavky, zmeny stavu a beh API pres Aspire/PostgreSQL.
- **Odevzdani:** Odevzdavat se maji pouze zdrojove kody. Do odevzdani nepatri `bin`, `obj`, `.vs`, lokalni databaze, logy ani jine docasne soubory.
- **Zname problemy:** Pri vyvoji bylo potreba sladit beh Aspire, PostgreSQL a Keycloaku. Pro jednodussi lokalni testovani je proto dostupny fallback na SQLite a vyvojova hlavicka `X-Debug-Role`.

## Spusteni testu

```powershell
dotnet test .\UTB.Minute.WebApi.Tests\UTB.Minute.WebApi.Tests.csproj
```

Testy spousti `UTB.Minute.AppHost`, pouzivaji Aspire PostgreSQL resource a volaji Web API pres HTTP. Docker Desktop nebo Podman musi byt spusteny.

## Seznam API endpointu

- `GET /api/dishes` - seznam jidel.
- `GET /api/dishes/{id}` - detail jidla.
- `POST /api/dishes` - vytvoreni jidla, role `Manager`.
- `PUT /api/dishes/{id}` - uprava jidla, role `Manager`.
- `GET /api/menu` - seznam polozek menu, volitelne s filtrem `date`.
- `GET /api/menu/{id}` - detail polozky menu.
- `POST /api/menu` - vytvoreni polozky menu, role `Manager`.
- `PUT /api/menu/{id}` - uprava polozky menu, role `Manager`.
- `DELETE /api/menu/{id}` - smazani polozky menu, role `Manager`.
- `GET /api/orders?includeCompleted=false` - seznam objednavek pro kuchyn, role `Kitchen`.
- `GET /api/orders/{id}` - detail objednavky pro kuchyn, role `Kitchen`.
- `GET /api/student/orders` - verejny seznam poslednich objednavek pro studenty.
- `POST /api/orders` - vytvoreni objednavky studentem.
- `PUT /api/orders/{id}/status` - zmena stavu objednavky, role `Kitchen`.
- `GET /api/orders/events` - SSE stream zmen objednavek.
