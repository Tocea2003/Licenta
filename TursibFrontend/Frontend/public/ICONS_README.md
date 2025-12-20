# PWA Icons

Pentru generarea icoanelor PNG din SVG, poți folosi:

## Opțiune 1: Online (simplu)
1. Deschide icon.svg în browser
2. Folosește https://realfavicongenerator.net/ sau https://favicon.io/
3. Upload icon.svg și generează toate dimensiunile

## Opțiune 2: CLI (profesional)
```bash
npm install -g sharp-cli
sharp -i icon.svg -o icon-72x72.png resize 72 72
sharp -i icon.svg -o icon-96x96.png resize 96 96
sharp -i icon.svg -o icon-128x128.png resize 128 128
sharp -i icon.svg -o icon-144x144.png resize 144 144
sharp -i icon.svg -o icon-152x152.png resize 152 152
sharp -i icon.svg -o icon-192x192.png resize 192 192
sharp -i icon.svg -o icon-384x384.png resize 384 384
sharp -i icon.svg -o icon-512x512.png resize 512 512
```

## Opțiune 3: Inkscape (gratuit)
```bash
inkscape icon.svg --export-type=png --export-filename=icon-192x192.png -w 192 -h 192
```

Pentru moment, poți copia favicon.ico existent sau crea placeholder-e temporare.
