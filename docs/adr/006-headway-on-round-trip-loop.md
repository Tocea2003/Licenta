# ADR-006: Headway măsurat pe bucla dus-întors, alerte cu histerezis

- **Stare:** Acceptat
- **Data:** 2026-11-17

## Context
Bunching-ul se definește prin goluri prea mici între autobuze consecutive ale aceleiași linii. Pozițiile vin ca GPS,
fără distanța parcursă pe traseu, iar autobuzele circulă în ambele sensuri.

## Decizie
- Fiecare poziție se proiectează pe forma liniei (`RouteGeometry`). Un indiciu per autobuz (poziția anterioară) evită
  săritura pe cealaltă trecere a traseelor care se apropie de ele însele.
- Autobuzul se așază pe **bucla dus-întors** de lungime 2L (dus = distanța, întors = 2L − distanța), deci toate
  autobuzele unei linii sunt ordonate pe un singur cerc. Golul față de cel din față se compară cu idealul 2L/n.
- O alertă apare sub 0,35 din ideal, dacă persistă 10 s, și se închide abia peste 0,6 (**histerezis**). Sub 0,15 e
  critică.

## Consecințe
- (+) Aceeași măsurătoare servește alertele live, digital twin-ul și controlul (o singură definiție a golului).
- (+) Histerezisul și persistența elimină alertele intermitente de la opririle în stații.
- (−) Presupune că toate autobuzele unei linii parcurg aceeași formă; variantele de traseu nu sunt modelate.
