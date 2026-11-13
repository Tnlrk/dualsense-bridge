# DualSense Bridge

Aplikace v oznamovací oblasti (vedle hodin), která z PS5 ovladače **DualSense** (a DualSense Edge) udělá
**virtuální Xbox 360 ovladač**. Funguje tak i ve hrách, které PS5 ovladač nepodporují.

## Co umí
- **Moderní okno nastavení** (vše na jedné stránce, změny se projeví hned) s **testem tlačítek a mapování**: na fotce ovladače se stisknutá tlačítka
  rozsvítí, ukazuje polohu páček a triggerů a u každého tlačítka je štítek s Xbox tlačítkem, které posílá.
- USB i Bluetooth, více ovladačů najednou (každý dostane vlastní Xbox ovladač).
- Vibrace ze hry (zap/vyp). Síla přesně podle hry, řídí se tedy nastavením vibrací ve hře.
- **Adaptivní triggery L2/R2**: odpor, spoušť s cvaknutím, vibrace v triggeru. Navíc volba
  „Vibrace triggerů podle hry“: triggery vibrují spolu s vibracemi hry.
- Barva světelné lišty, LEDky s číslem hráče, červená lišta při slabé baterii.
- Ikona v liště ukazuje **baterii** (barevný sloupec), **nabíjení** (blesk) a **spojení** (BT/USB).
  Windows tě upozorní na připojení, odpojení, slabou baterii a plné nabití.
- Volitelné skrytí originálního ovladače před hrami (HidHide), aby se vstupy nezdvojovaly.
- Spouštění s Windows.
- **Čeština / English** – přepínač jazyka v okně nastavení (CZ | EN) i v menu v liště; výchozí podle jazyka Windows.
- **Nápověda přímo v aplikaci** – tlačítko „Nápověda“ v nastavení nebo v menu v liště.

## Mapování tlačítek
| DualSense | Xbox |
|---|---|
| ✕ / ○ / □ / △ | A / B / X / Y |
| L1 / R1 | LB / RB |
| L2 / R2 | LT / RT (analogově) |
| L3 / R3 | LS / RS |
| Create | Back / View |
| Options | Start / Menu |
| PS | Guide (Xbox tlačítko) |
| Klik na touchpad | Back (dá se vypnout) |

## Instalace na jiný PC
1. Zkopíruj `dist\DualSenseBridge.exe` (třeba na flashku) a spusť ho.
2. Pokud na PC chybí ovladač **ViGEmBus**, aplikace ho nabídne nainstalovat (je přibalený, potvrdíš UAC).
3. Při prvním spuštění nabídne i **HidHide** (skrývání originálu). Po instalaci může být potřeba restart.
4. V menu v liště zapni **Spouštět s Windows**. Exe se zkopíruje do `%LocalAppData%\DualSenseBridge`,
   takže původní soubor můžeš smazat.

.NET instalovat není potřeba, je součástí exe.

## Tipy
- **Steam**: pokud hraješ přes Steam, vypni v nastavení ovladačů „PlayStation Controller Support“,
  jinak Steam DualSense zachytí také.
- **Skrytí originálu** zůstává aktivní i po ukončení aplikace. Dokud je zapnuté, uvidí PS5 ovladač jen
  tato aplikace. Hry s nativní podporou PS5 (vč. adaptivních triggerů ze hry) ho pak neuvidí.
  Vypneš ho v menu (zruš zaškrtnutí „Skrýt originální PS5 ovladač…“).
- Adaptivní triggery nastavené v aplikaci platí pro všechny hry. Xbox hry triggerům vlastní efekty neposílají.

## Vývoj
```
dotnet test          # testy protokolu (parser, výstupní reporty, CRC32, mapování)
./build.ps1          # testy + přenosný dist\DualSenseBridge.exe
```
Struktura: `DualSense/` (HID protokol), `Bridge/` (ViGEm + správa zařízení), `HidHide/`, `Setup/`
(přibalené instalátory ovladačů v `Redist/`), `UI/` (ikona v liště, nastavení).

Ovladače ViGEmBus a HidHide jsou od Nefarius Software Solutions (BSD-3).
Fotku ovladače (`src/DualSenseBridge/Assets/dualsense.png`) dodal uživatel. Jde o produktovou fotku
třetí strany, proto ji při veřejném šíření aplikace nahraď vlastní fotkou.

Náhledy vykreslení (bez spuštění aplikace): `$env:PREVIEW_DIR="C:\nejaka\slozka"; dotnet test --filter PreviewRender`.
