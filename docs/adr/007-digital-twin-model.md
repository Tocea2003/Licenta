# ADR-007: Digital twin: model agregat de pasageri, determinist

- **Stare:** Acceptat
- **Data:** 2026-11-21

## Context
Twin-ul trebuie să evalueze scenarii „what-if” și strategii de control înainte de aplicare, de multe ori (sute de
rulări pentru o comparație și mii pentru antrenarea RL), deci trebuie să fie rapid și reproductibil. Bunching-ul trebuie
să apară din dinamică, nu să fie impus.

## Decizie
- O linie e o buclă cu stații echidistante. Autobuzele nu se depășesc. Pasul de simulare e de 1 s, iar 4 h simulate
  durează milisecunde.
- **Pasagerii sunt modelați ca fluxuri:** sosesc uniform în fiecare stație (rata λ). Un autobuz care vine după un headway
  H găsește λH pasageri, care au așteptat λH²/2 pasager·secunde. Timpul de oprire crește cu numărul de pasageri, iar
  din această reacție apare bunching-ul. Fiecare autobuz are o încărcătură (urcă cei din stație, coboară o fracțiune la
  fiecare stație), folosită pentru costul holding-ului suportat de pasagerii din autobuz.
- Metrici: așteptarea medie E[H²]/(2E[H]), așteptarea în exces Var(H)/(2E[H]), CV-ul headway-ului și întârzierea totală
  pe pasager (așteptare + timp pierdut în autobuz din holding).
- What-if: frecvență (numărul de autobuze calculat din tura estimată), autobuze adăugate în cel mai mare gol, străzi
  închise (porțiune pe ambele sensuri, ocol ca viteză efectivă redusă, stații sărite, cererea lor mutată la stațiile
  vecine), autobuze oprite forțat.
- Totul e **determinist pentru un seed**. Seed-urile de antrenare, validare și test sunt disjuncte.

## Consecințe
- (+) Comparațiile sunt pe perechi: aceleași seed-uri pentru fiecare strategie, deci diferențele vin din strategie.
- (+) Rapid: experimentul complet (6 scenarii × 4 strategii × 20 de seed-uri) rulează în ~1 minut.
- (−) Nu modelează pasageri individuali (origine-destinație), transferuri, variația cererii în timpul zilei sau trafic
  real. Valorile absolute sunt ale modelului; concluziile se referă la diferențele relative dintre strategii.
