# Google OAuth 2.0 Setup Guide

## Pasul 1: Creează un proiect în Google Cloud Console

1. Accesează [Google Cloud Console](https://console.cloud.google.com/)
2. Fă click pe dropdown-ul de proiecte (lângă logo-ul Google Cloud)
3. Click pe "NEW PROJECT"
4. Nume proiect: "Tursib App" (sau orice alt nume)
5. Click "CREATE"

## Pasul 2: Activează Google Sign-In API

1. În panoul de navigare din stânga, mergi la "APIs & Services" > "Library"
2. Caută "Google Identity"
3. Click pe "Google Identity Toolkit API" sau "Google+ API"
4. Click "ENABLE"

## Pasul 3: Configurează OAuth Consent Screen

1. Mergi la "APIs & Services" > "OAuth consent screen"
2. Selectează "External" (dacă nu ai Google Workspace)
3. Click "CREATE"
4. Completează formul:
   - **App name**: Tursib
   - **User support email**: email-ul tău
   - **Developer contact information**: email-ul tău
5. Click "SAVE AND CONTINUE"
6. La "Scopes", nu e nevoie să adaugi scope-uri custom, doar click "SAVE AND CONTINUE"
7. La "Test users", adaugă email-ul tău pentru testare
8. Click "SAVE AND CONTINUE"

## Pasul 4: Creează OAuth 2.0 Client ID

1. Mergi la "APIs & Services" > "Credentials"
2. Click "CREATE CREDENTIALS" > "OAuth client ID"
3. Application type: **Web application**
4. Name: "Tursib Web Client"
5. **Authorized JavaScript origins**:
   - `http://localhost:5173` (pentru development)
   - `http://192.168.1.XXX:5173` (înlocuiește cu IP-ul tău local dacă testezi de pe telefon)
   - Adaugă și domeniul de producție când o să ai
6. **Authorized redirect URIs**:
   - `http://localhost:5173` (pentru development)
   - Același lucru pentru alte environment-uri
7. Click "CREATE"
8. **IMPORTANT**: Copiază **Client ID** (arată ca: `123456789-abcdefgh.apps.googleusercontent.com`)

## Pasul 5: Configurează aplicația

### Backend (TursibBackend)
Nu e nevoie de configurare suplimentară - backend-ul doar validează token-urile primite de la frontend.

### Frontend (TursibFrontend)

1. Deschide fișierul `.env`:
   ```bash
   cd d:\Licenta\TursibFrontend\Frontend
   notepad .env
   ```

2. Înlocuiește `your-google-client-id-here.apps.googleusercontent.com` cu Client ID-ul tău real:
   ```env
   VITE_GOOGLE_CLIENT_ID=123456789-abcdefgh.apps.googleusercontent.com
   ```

3. Salvează fișierul

## Pasul 6: Testează autentificarea

1. **Pornește backend-ul** (dacă nu rulează deja):
   ```powershell
   cd d:\Licenta\TursibBackend
   dotnet run
   ```

2. **Pornește frontend-ul**:
   ```powershell
   cd d:\Licenta\TursibFrontend\Frontend
   npm run dev
   ```

3. **Deschide aplicația** în browser: `http://localhost:5173`

4. **Navighează la Login**: Click pe butonul de login

5. **Click pe "Continuă cu Google"**:
   - Ar trebui să apară popup-ul Google
   - Selectează contul tău
   - Confirmă permisiunile
   - Ar trebui să fii autentificat automat

## Verificări

### ✅ Autentificare reușită
- Ar trebui să fii redirectionat la pagina principală
- Username-ul tău ar trebui să apară în meniul de utilizator
- Token-ul JWT ar trebui să fie salvat în localStorage

### ❌ Probleme comune

**Eroare: "Token Google invalid"**
- Verifică că Client ID-ul din `.env` este corect
- Verifică că ai activat Google Identity API în Cloud Console

**Eroare: "redirect_uri_mismatch"**
- Verifică că URL-ul din browser (`http://localhost:5173`) este adăugat în "Authorized JavaScript origins" și "Authorized redirect URIs"
- Asigură-te că nu ai trailing slash (`/`) la sfârșitul URL-urilor

**Popup-ul Google nu apare**
- Verifică consola browser-ului pentru erori JavaScript
- Asigură-te că Google Identity Services script se încarcă (verifică Network tab)
- Verifică că browser-ul permite popup-uri

**Backend returnează eroare 500**
- Verifică logs-urile backend-ului în terminal
- Asigură-te că migrația pentru câmpul `Email` a fost aplicată: `dotnet ef database update`

## Configurare pentru producție

Când vei deploia aplicația în producție:

1. Adaugă domeniul tău în "Authorized JavaScript origins":
   - Exemplu: `https://tursib.ro`

2. Actualizează `.env` cu Client ID-ul de producție (sau folosește același)

3. Treci OAuth Consent Screen de la "Testing" la "Production" în Google Cloud Console

## Securitate

✅ **Bune practici implementate**:
- Token-ul Google este validat pe backend înainte de a crea sesiunea
- Nu stocăm parola pentru utilizatorii Google (PasswordHash = "")
- JWT token-ul local expiră după 7 zile
- Email-ul este stocat pentru identificare unică

⚠️ **Recomandări suplimentare**:
- Nu partaja Client ID-ul de producție public
- Folosește HTTPS în producție
- Implementează rate limiting pe endpoint-ul `/api/auth/google`
- Adaugă logging pentru tentative de autentificare eșuate

## Debugging

Pentru a vedea ce se întâmplă în timpul autentificării:

1. **Frontend**: Deschide Console-ul browser-ului (F12)
   - Căută log-uri cu `✅` (success) și `❌` (errors)
   - Verifică că token-ul Google este trimis la backend

2. **Backend**: Verifică terminal-ul unde rulează `dotnet run`
   - Ar trebui să vezi log-uri despre crearea utilizatorului
   - Exemplu: `✅ Created new Google user: john.doe (john.doe@gmail.com)`

## Documentație oficială

- [Google Identity Services](https://developers.google.com/identity/gsi/web/guides/overview)
- [Google OAuth 2.0](https://developers.google.com/identity/protocols/oauth2)
- [Google Cloud Console](https://console.cloud.google.com/)

---

**Status**: ✅ Implementare completă
**Ultimul update**: 16 ianuarie 2025
