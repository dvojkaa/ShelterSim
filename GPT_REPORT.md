# Předávací report GPT — 2026-11-08

Práce na tomto kole je dokončená a zastavená na žádost uživatele. Žádné další funkce nerozpracovávám.

## Co přibylo

1. **Lokace:** nemocnice, továrna, benzínka, farma, vojenské stanoviště a opuštěný bunker.
   Rozdílná geometrie, přístupné interiéry, specializovaný loot, objevy a vzácné expediční součástky.
2. **Krajina:** říční údolí, mělká jezera, horská sedla, plynulejší změny výšek mezi biomy,
   dálniční mosty a přípojky měst na hlavní silnice. Voda je mělká a průchozí, bez plavání.
3. **Živý svět:** jeleni, divočáci a ptáci s pohybem/útěkem, hlídky raiderů a opuštěné zásobovací bedny.
   Poškození a porážka generovaných hlídek přežijí odložení chunku i save/load.
4. **Postup:** konzole Expedition Workshop uvnitř bunkru. Objevy, součástky a materiály odemykají
   izolované oblečení (-65 % expozice), úsporné dávky (-20 % základní spotřeby) a trénink (+20 % sprint).
5. **Atmosféra — logika:** uložitelný dvacetiminutový den, Clear/Fog/Rain/Snow, intenzita,
   přístřeší a mírné vlivy biomu/noci/počasí na potřeby. Vzhled a zvuk počasí patří Claudovi.

Základem je generování 128m chunků během průzkumu, šest biomů, městské čtvrti, atlas na M,
omezený počet načtených chunků a ukládání změněného obsahu. Bunker a staré okolí zůstávají zachované.

## Co je skutečně ověřené

Finální kontrola běžela v oddělené kopii `/tmp/sheltersim-verify`, nikoli přestavbou sdílené scény.

| Kontrola | Výsledek |
|---|---|
| Kompletní setup M1–M19 + konstrukční/ekonomické kontroly | prošlo |
| Linux build | prošel, 112981277 bajtů |
| `--smoke-test` | ALL CHECKS PASSED, exit 0 |
| `--world-probe` | ALL CHECKS PASSED, exit 0 |
| `--expedition-probe` | ALL CHECKS PASSED, exit 0 |

Testy pokrývají obnovu po smrti, opakované načtení, zálohu poškozeného savu, inventář/náhledy,
šest biomů, skutečné odkládání chunků, návštěvu vytěžené suroviny, terénní kolize, městské dveře,
všech šest lokací včetně přístupu k bednám a rampám, vodní hloubky, čas/počasí během pauzy,
ceny a účinky vylepšení i přetrvání poškozených a poražených hlídek. Save/load navíc kontroluje
objevy, vylepšení a světový čas. Nezávislá kontrola kódu nenašla blokující chybu.

Logy:
- `/tmp/sheltersim-final-build.log`
- `/tmp/sheltersim-final-smoke.log`
- `/tmp/sheltersim-final-world.log`
- `/tmp/sheltersim-final-expedition.log`

Headless prostředí hlásilo nedostupný zvukový výstup FMOD; není to poslechový test.
Nový grafický/human playtest v tomto kole neproběhl. Testy nezaručují výslednou hratelnost,
vizuální kvalitu, zvuk ani výkon na cílovém hardwaru.

## Spuštění a předání

Aktualizovaný ověřený spustitelný soubor: `Builds/Linux/SheltreSim.x86_64`.

Sdílenou scénu a existující prefab/ProjectSettings změny jsem nepřepisoval. Pro hraní nové verze
v Editoru otevři současnou scénu a spusť `Tools > SheltreSim > Setup M19 Expeditions`, pokud
má M18 už zapojené. Tím se přidá konzole, progression, simulace a nové reference bez celého art passu.
Pro kompletní generaci použij běžný setup v kopii projektu. M18 už nepřepisuje styl existujících
biomových materiálů ani mlhu/kameru. Nové mapové assety byly doplněny pouze tam, kde neexistovaly.

Save formát v4 čte také staré v0/v2/v3. Říční údolí mění některé výšky generovaného terénu:
u starého savu mimo bunker ověř polohu hráče a dříve postavených dílů. Automatická migrace staveb
na nový terén není implementovaná. Vzdálené chunky pozastavují lokální regrowth; floating origin není.

Drobnost ponechaná po review: přípojka přicházející od jihu může mít v křižovatce malé vizuální
překrytí s dálnicí. Průchod a kolize nejsou blokované. Zvířata mají základní generovanou geometrii;
jejich finální vzhled může Claude nahradit bez změny chování.

## Koordinace s Claudem

Společný mailbox: `DEV_COORDINATION.md`. Claude potvrdil rozdělení vlastnictví. GPT neupravoval
jeho zvukové soubory; našel a předal chyby kompilace, které Claude opravil. Finální build obsahoval
společně zkopírované aktuální zvukové skripty/assety. Poslední další změny Clauda po této kopii nejsou
součástí tohoto výsledku. Přesná rozhraní počasí, vody, zvířat a herních událostí jsou v mailboxu.

Změny zůstávají v pracovním stromu; neprováděl jsem commit/push smíchané práce více autorů.
