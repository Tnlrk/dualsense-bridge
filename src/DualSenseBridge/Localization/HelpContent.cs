namespace DualSenseBridge.Localization;

/// <summary>Obsah nápovědy v obou jazycích.</summary>
public static class HelpContent
{
    public abstract record Block;
    public sealed record Text(string Value) : Block;
    public sealed record Bullets(params string[] Items) : Block;
    /// <summary>Tabulka mapování tlačítek (DualSense → Xbox).</summary>
    public sealed record MappingTable(string LeftHeader, string RightHeader, params (string Ds, string Xbox)[] Rows) : Block;

    public sealed record Section(string Title, params Block[] Blocks);

    public static IReadOnlyList<Section> For(string language) => language == Loc.English ? English : Czech;

    private static readonly (string, string)[] Mapping =
    {
        ("✕  /  ○  /  □  /  △", "A  /  B  /  X  /  Y"),
        ("L1  /  R1", "LB  /  RB"),
        ("L2  /  R2", "LT  /  RT"),
        ("L3  /  R3", "LS  /  RS"),
        ("D-pad", "D-pad"),
        ("Create", "Back / View"),
        ("Options", "Start / Menu"),
        ("PS", "Guide (Xbox)"),
        ("Touchpad", "Back / View"),
    };

    private static readonly Section[] Czech =
    {
        new("Jak to funguje",
            new Text("DualSense Bridge čte tvůj PS5 ovladač (DualSense) a vytváří z něj virtuální Xbox 360 ovladač. " +
                     "Hry, které podporují Xbox ovladač, ho tak používají bez dalšího nastavení."),
            new Bullets(
                "Aplikace běží v oznamovací oblasti vedle hodin. Dvojklik na ikonu otevře nastavení, pravé tlačítko menu.",
                "Všechna nastavení se projeví okamžitě a ukládají se automaticky.",
                "Funguje přes USB kabel i Bluetooth a s více ovladači najednou (každý dostane vlastní Xbox ovladač).")),

        new("První spuštění",
            new Bullets(
                "Bluetooth: Nastavení Windows → Bluetooth a zařízení → Přidat zařízení. Na ovladači podrž Create + PS, dokud lišta nezačne rychle blikat.",
                "USB: stačí připojit kabel.",
                "Pokud na počítači chybí ovladač ViGEmBus, aplikace ho sama nabídne nainstalovat (je přibalený, Windows se zeptá na práva správce).",
                "Volitelně nabídne i HidHide, který skryje originální PS5 ovladač před hrami (viz níže).")),

        new("Ikona v liště",
            new Bullets(
                "Barevný sloupec = baterie: zelená nad 50 %, oranžová 20–50 %, červená pod 20 %.",
                "Blesk = ovladač se nabíjí.",
                "BT / USB = jak je ovladač připojený.",
                "Šedá přeškrtnutá baterie = žádný ovladač není připojený.",
                "Červený vykřičník = chybí ovladač ViGEmBus (nainstaluješ ho z menu).")),

        new("Mapování tlačítek",
            new MappingTable("DualSense", "Xbox", Mapping),
            new Text("Kliknutí na touchpad jako Back jde vypnout v sekci Ostatní. V testu tlačítek vidíš u každého tlačítka, co se posílá do hry.")),

        new("Adaptivní triggery",
            new Text("Xbox hry triggerům žádné efekty neposílají, proto si efekt nastavíš sám – platí ve všech hrách:"),
            new Bullets(
                "Odpor – trigger tlačí proti prstu od zvolené pozice až na konec.",
                "Spoušť – odpor mezi začátkem a „cvaknutím“, pak povolí (jako mačkání spouště zbraně).",
                "Vibrace – trigger vibruje od zvolené pozice, nastavíš sílu i frekvenci.",
                "Pozice 0 = úplně nahoře, 9 = skoro dole. Síla 1–8."),
            new Text("Změna se pošle do ovladače hned – stačí zmáčknout L2/R2 a vyzkoušet.")),

        new("Vibrace",
            new Bullets(
                "Síla vibrací odpovídá přesně tomu, co posílá hra – řídí se tedy nastavením vibrací ve hře.",
                "„Vibrace triggerů podle hry“ – Xbox 360 ovladač vibrace triggerů nedostává, takže se odvozují z běžných vibrací hry.",
                "„Jen zmáčknutý trigger“ – vibruje jen ten trigger, který držíš (při brzdění L2, při plynu R2).",
                "Tlačítko „Vyzkoušet vibrace“ krátce rozvibruje ovladač.")),

        new("Světelná lišta",
            new Bullets(
                "Barvu vybereš kliknutím na kroužek nebo posuvníky Odstín a Sytost – změna je vidět hned.",
                "Jas 1–100 %, případně lištu úplně vypneš přepínačem „Svítit“.",
                "Při slabé baterii může lišta svítit červeně (i když je vypnutá).",
                "LEDky pod touchpadem ukazují číslo hráče jako na PS5.")),

        new("Skrytí originálu (HidHide)",
            new Text("Některé hry vidí zároveň PS5 ovladač i virtuální Xbox ovladač a vstupy se zdvojí. HidHide originál před hrami schová."),
            new Bullets(
                "Zapneš ho v menu v liště: „Skrýt originální PS5 ovladač před hrami“ (Windows se zeptá na práva správce).",
                "Skrytí zůstává aktivní i po ukončení aplikace – PS5 ovladač pak vidí jen tato aplikace.",
                "Hry s nativní podporou PS5 (vč. adaptivních triggerů od hry) ho pak neuvidí. Pro ně skrývání vypni.",
                "Pokud hra už běží, po zapnutí skrytí ji restartuj.")),

        new("Steam",
            new Text("Steam umí PS5 ovladač zachytávat také. Pokud hraješ přes Steam a vstupy jsou zdvojené, " +
                     "vypni v nastavení Steamu → Ovladač podporu PlayStation ovladačů, nebo zapni skrytí originálu.")),

        new("Jiný počítač a spouštění s Windows",
            new Bullets(
                "Celá aplikace je jeden soubor DualSenseBridge.exe – stačí ho zkopírovat a spustit. .NET instalovat není potřeba.",
                "Chybějící ovladače (ViGEmBus, HidHide) jsou přibalené a aplikace je sama nabídne.",
                "„Spouštět s Windows“ v menu zkopíruje aplikaci do složky AppData a zapne automatické spuštění.")),

        new("Řešení problémů",
            new Bullets(
                "Hra ovladač nevidí – podívej se do testu tlačítek, jestli se tlačítka rozsvěcují. Pak hru restartuj.",
                "Vstupy se zdvojují – zapni skrytí originálu (HidHide) nebo vypni podporu PlayStation ovladačů ve Steamu.",
                "Nevibruje – zkontroluj vibrace v nastavení hry a přepínač „Vibrace ze hry“; vyzkoušej tlačítkem „Vyzkoušet vibrace“.",
                "Baterie ukazuje „zjišťuji…“ – u Bluetooth to po připojení chvíli trvá.",
                "„Ovladač nejde otevřít“ – odpoj a znovu připoj ovladač; pomůže i restart aplikace.",
                "Ikona s červeným vykřičníkem – nainstaluj ViGEmBus z menu v liště.")),
    };

    private static readonly Section[] English =
    {
        new("How it works",
            new Text("DualSense Bridge reads your PS5 controller (DualSense) and turns it into a virtual Xbox 360 controller, " +
                     "so games that support Xbox controllers use it without any extra setup."),
            new Bullets(
                "The app runs in the notification area next to the clock. Double-click the icon to open settings, right-click for the menu.",
                "All settings apply instantly and are saved automatically.",
                "Works over USB and Bluetooth, and with several controllers at once (each gets its own Xbox controller).")),

        new("First start",
            new Bullets(
                "Bluetooth: Windows Settings → Bluetooth & devices → Add device. Hold Create + PS on the controller until the light bar blinks quickly.",
                "USB: just plug in the cable.",
                "If the ViGEmBus driver is missing, the app offers to install it (it is bundled; Windows asks for administrator permission).",
                "It can also offer HidHide, which hides the original PS5 controller from games (see below).")),

        new("Tray icon",
            new Bullets(
                "Coloured bar = battery: green above 50 %, orange 20–50 %, red below 20 %.",
                "Lightning bolt = the controller is charging.",
                "BT / USB = how the controller is connected.",
                "Grey crossed-out battery = no controller connected.",
                "Red exclamation mark = the ViGEmBus driver is missing (install it from the menu).")),

        new("Button mapping",
            new MappingTable("DualSense", "Xbox", Mapping),
            new Text("Touchpad click as Back can be turned off under Other. The button test shows what each button sends to the game.")),

        new("Adaptive triggers",
            new Text("Xbox games don't send trigger effects, so you set your own – it applies in every game:"),
            new Bullets(
                "Resistance – the trigger pushes back from the chosen position to the end.",
                "Weapon – resistance between the start and the “click”, then it gives way (like pulling a gun trigger).",
                "Vibration – the trigger vibrates from the chosen position; you set strength and frequency.",
                "Position 0 = fully released, 9 = almost fully pressed. Strength 1–8."),
            new Text("Changes are sent to the controller instantly – just press L2/R2 to try them.")),

        new("Vibration",
            new Bullets(
                "Vibration strength is exactly what the game sends – it follows the game's own vibration setting.",
                "“Trigger vibration from games” – an Xbox 360 controller doesn't receive trigger vibration, so it is derived from the regular game vibration.",
                "“Only the pressed trigger” – only the trigger you hold vibrates (L2 when braking, R2 on throttle).",
                "The “Test vibration” button briefly rumbles the controller.")),

        new("Light bar",
            new Bullets(
                "Pick a colour by clicking a swatch or using the Hue and Saturation sliders – changes show instantly.",
                "Brightness 1–100 %, or switch the light bar off completely with “On”.",
                "On low battery the light bar can turn red (even when it is switched off).",
                "The LEDs below the touchpad show the player number, just like on a PS5.")),

        new("Hiding the original (HidHide)",
            new Text("Some games see both the PS5 controller and the virtual Xbox controller and the input gets doubled. HidHide hides the original from games."),
            new Bullets(
                "Turn it on in the tray menu: “Hide the original PS5 controller from games” (Windows asks for administrator permission).",
                "Hiding stays active even after the app exits – only this app can see the PS5 controller then.",
                "Games with native PS5 support (including adaptive triggers from the game) won't see it either. Turn hiding off for them.",
                "If a game is already running, restart it after enabling hiding.")),

        new("Steam",
            new Text("Steam can capture the PS5 controller too. If you play through Steam and the input is doubled, " +
                     "turn off PlayStation controller support in Steam → Settings → Controller, or enable hiding the original.")),

        new("Another PC and starting with Windows",
            new Bullets(
                "The whole app is a single DualSenseBridge.exe file – just copy it and run it. No need to install .NET.",
                "Missing drivers (ViGEmBus, HidHide) are bundled and the app offers to install them.",
                "“Start with Windows” in the menu copies the app to the AppData folder and enables automatic start.")),

        new("Troubleshooting",
            new Bullets(
                "The game doesn't see the controller – check that buttons light up in the button test, then restart the game.",
                "Doubled input – enable hiding the original (HidHide) or turn off PlayStation controller support in Steam.",
                "No vibration – check vibration in the game's settings and the “Vibration from games” switch; try “Test vibration”.",
                "Battery shows “checking…” – over Bluetooth it takes a moment after connecting.",
                "“Cannot open the controller” – disconnect and reconnect the controller; restarting the app also helps.",
                "Icon with a red exclamation mark – install ViGEmBus from the tray menu.")),
    };
}
