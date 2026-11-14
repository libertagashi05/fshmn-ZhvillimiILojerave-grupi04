# Tempulli i Humbur

Lojë vrapimi pa fund (endless runner) në stilin e Temple Run 2, e bërë me **Unity 6 (6000.6.6f1, URP)** dhe **Blender**.

## Si hapet
1. Instalo Unity **6000.6.6f1** nga Unity Hub.
2. Unity Hub → **Add** → zgjidh këtë dosje.
3. Hape projektin (herën e parë zgjat disa minuta, Unity krijon dosjen `Library`).
4. Menuja **Tempulli → Ndërto skenën e lojës**, pastaj **Play**.

## Kontrollet
- ← → / A D: anohu majtas-djathtas, kthehu në kthesa
- ↑ / W / Space: kërce
- ↓ / S: rrëshqit
- Në telefon: rrëshqit gishtin, anoje telefonin

## Dosjet
- `Assets/Scripts` – loja (lojtari, shtegu, kamera, animacionet)
- `Assets/Editor` – ndërtimi i skenës dhe importi i Artës
- `Assets/Models` – Arta (`arta.fbx`) dhe natyra
- `Assets/Shaders`, `Assets/VFX` – Shader Graph dhe VFX Graph
- `Blender` – skedarët burimorë të Blender dhe skriptet që i krijojnë

## Paketat
URP, Shader Graph, VFX Graph, ProBuilder, Animation Rigging, Cinemachine, Input System.

## Fuqitë
Dalin si objekte që shkëlqejnë mbi shteg (në pjesët pa pengesa):
- **Magnet** (i kuq, 10 s): monedhat afër vijnë vetë te Arta.
- **Mburojë** (e kaltër): të shpëton një herë nga përplasja (flluskë drite rreth Artës).
- **Vrap i shpejtë** (i verdhë-gjelbër, 5 s): ~1.8x më shpejt, pengesat thyhen, kthesat merren vetë.

## Portalet dhe botët
Menuja **Tempulli → Ndërto botët** krijon `Bota_Nata`, `Bota_Akulli` dhe `Bota_Vullkani` (dhe i shton te Build Settings).
Pastaj, pas çdo ~500 m del një portal; ngjyra e vorbullës tregon ku të çon
(e artë = tempulli, vjollcë = nata, e kaltër = akulli, portokalli = vullkani). Pikët, distanca dhe monedhat ruhen.
Temat janë te `Assets/Botet/Tema_*.asset` (mund t'i ndryshosh në Inspector dhe ta ndërtosh sërish botën).
