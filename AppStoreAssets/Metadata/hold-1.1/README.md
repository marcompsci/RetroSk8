# Held for 1.1

Retro Sk8 1.0 ships in English only. The Spanish (es-ES, es-MX) and Japanese (ja) listing text here, and the in-game
translations (`Core/LocTables.cs`), wait until a native speaker of each has checked them on a device.

To ship them in 1.1:
1. Native speakers turn on the preview with the code `LANG PREVIEW` (Codes screen), play, and send corrections.
2. Fix `Tools/loc/es.tsv` / `ja.tsv`, run `python3 Tools/loc/build_tables.py`.
3. Flip Spanish and Japanese to `true` in `Core/LangRelease.cs` (`Shipped`) and update `LangReleaseTests`.
4. Move these folders back up to `AppStoreAssets/Metadata/`, re-add the language sentence to the English description,
   and add the three localizations in App Store Connect with the `es` / `ja` screenshots.
