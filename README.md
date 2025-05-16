FieldAnalysis program működési leírás

Ez a C# program egy térinformatikai (GIS) elemző alkalmazás, amely raszteres adatok feldolgozására és klaszterezésére szolgál, majd az eredményeket GeoJSON formátumban menti el. Az alábbiakban összefoglalom, mit csinál a program lépésről lépésre:

Adatok kinyerése PostgreSQL adatbázisból (FetchData):
Kapcsolódik egy PostgreSQL adatbázishoz az Npgsql könyvtár segítségével.
Lekéri a „Debreceni példa” nevű raszter adatot a grids táblából.
A raszter egy 512x512-es kétdimenziós tömb, amely egész számokat tartalmaz.
Ellenőrzi, hogy az adatok mérete megfelelő-e, és hogy nem tartalmaznak-e tört számokat.
Kiírja a raszter minimum és maximum értékeit, valamint jelzi, ha tört számokat talál.
Raszter kép mentése (SaveRasterImage):
A kinyert raszter adatot szürkeárnyalatos PNG képként menti el (raster.png), ahol az értékek intenzitása határozza meg a szürke színt.
Klaszterezés (ClassifyCells):
K-means klaszterezést végez a raszter adatokon három klaszterrel.
Inicializálja a centroidokat véletlenszerűen, majd iteratívan frissíti őket:
Minden cellát a legközelebbi centroidhoz rendel.
Újraszámolja a centroidokat az egyes klaszterek átlagaként.
A folyamat addig ismétlődik, amíg a klaszter-hozzárendelések nem változnak.
Az eredmény egy 512x512-es címke tömb (labels), amely minden cellához egy klaszter indexet (0, 1 vagy 2) rendel, valamint a három centroid értéke.
Klaszter kép mentése (SaveClusterImage):
A klaszterezési eredményt színes PNG képként menti el (clusters.png), ahol a három klasztert piros, zöld és kék színek jelzik.
Klaszter eloszlás kiírása:
Összeszámolja, hány cella tartozik az egyes klaszterekhez, és kiírja az eloszlást.
Kiírja a centroidok értékeit is.
Polygonok generálása (GeneratePolygons):
A klaszterezett cellák alapján azonosítja az összefüggő régiókat (azonos klaszterű cellák csoportjait).
Egy mélységi keresési algoritmus (DFS) segítségével gyűjti össze az egyes régiók celláit.
Csak azokat a régiókat tartja meg, amelyek területe legalább 5000 cella (0,5 hektár, feltételezve, hogy 1 cella = 1 m²).
Az eredmény egy lista, amely tartalmazza a klaszter azonosítóját és a régió celláinak koordinátáit.
Multipolygonok létrehozása (CreateMultiPolygons):
Az összefüggő régiókat GeoJSON-kompatibilis multipolygonokká alakítja a NetTopologySuite segítségével.
Lekéri a raszter georeferenciális metaadatait (pixelméret, bal felső sarok koordinátái) az adatbázisból.
Minden cellát négyszögletes polygonként kezel, és valós világszerinti koordinátákra konvertálja.
A polygonokat klaszterenként csoportosítja, és multipolygonokat hoz létre.
Minden multipolygonhoz hozzárendeli a klaszter azonosítóját és a centroid értékét tulajdonságként.
GeoJSON mentése (SaveGeoJson):
A generált multipolygonokat GeoJSON formátumban menti el (output.geojson).
A GeoJSON fájl tartalmazza a geometriákat és a hozzájuk tartozó tulajdonságokat (klaszter ID, átlagos érték).
Hibakezelés:
A program minden lépésben kezeli a lehetséges hibákat (pl. adatbázis kapcsolódási hiba, érvénytelen raszter méret, típuskonverziós problémák), és hibaüzenetet ír ki, ha valami nem sikerül.
Összegzés
A program egy 512x512-es raszter adathalmazt elemez, amelyet egy PostgreSQL adatbázisból tölt be. Klaszterezést végez, hogy a cellákat három csoportba sorolja, majd az azonos klaszterű összefüggő régiókat polygonokká alakítja. Az eredményeket képekként (raszter és klaszter képek) és GeoJSON formátumban menti el. A program térinformatikai elemzésekhez használható, például mezőgazdasági területek vagy más térbeli adatok csoportosítására és vizualizálására.
