# Design System — Tursib Frontend

Document generat in cadrul audit-ului `/design-system`. Descrie inventarul actual al token-urilor, componentelor si claselor utilitare, impreuna cu gap-urile identificate si imbunatatirile aplicate.

> **Sursa single-of-truth**: `src/assets/main.css` (tokens semantice + clase utilitare) si `src/assets/base.css` (primitive + reset).

---

## 1. Design tokens

Toate valorile sunt expuse ca CSS custom properties si respecta schema **light → dark** prin selector-ul `.dark` aplicat pe body.

### 1.1. Culori

| Categorie | Token | Light | Dark |
|---|---|---|---|
| Background | `--bg-primary` | `#ffffff` | `#0f172a` |
| | `--bg-secondary` | `#f8fafc` | `#1e293b` |
| | `--bg-tertiary` | `#f1f5f9` | `#334155` |
| | `--bg-elevated` | `#ffffff` | `#1e293b` |
| | `--bg-overlay` | `rgba(15,23,42,0.5)` | `rgba(0,0,0,0.6)` |
| Text | `--text-primary` | `#0f172a` | `#f1f5f9` |
| | `--text-secondary` | `#475569` | `#94a3b8` |
| | `--text-tertiary` | `#94a3b8` | `#64748b` |
| | `--text-inverse` | `#ffffff` | `#0f172a` |
| | `--text-on-accent` | `#ffffff` | — |
| Border | `--border-primary` | `#e2e8f0` | `#334155` |
| | `--border-secondary` | `#cbd5e1` | `#475569` |
| | `--border-focus` | `#3b82f6` | `#60a5fa` |
| Accent | `--accent-primary` | `#3b82f6` | `#60a5fa` |
| | `--accent-primary-hover` | `#2563eb` | `#3b82f6` |
| | `--accent-primary-soft` | `#eff6ff` | `rgba(96,165,250,0.12)` |
| | `--accent-secondary` | `#8b5cf6` | `#a78bfa` |
| | `--accent-secondary-soft` | `#f5f3ff` | `rgba(167,139,250,0.12)` |
| Status | `--color-success` | `#22c55e` | `#4ade80` |
| | `--color-success-soft` | `#f0fdf4` | `rgba(74,222,128,0.12)` |
| | `--color-warning` | `#f59e0b` | `#fbbf24` |
| | `--color-warning-soft` | `#fffbeb` | `rgba(251,191,36,0.12)` |
| | `--color-danger` | `#ef4444` | `#f87171` |
| | `--color-danger-soft` | `#fef2f2` | `rgba(248,113,113,0.12)` |
| | `--color-info` | `#3b82f6` | `#60a5fa` |
| Route | `--route-red / blue / green / orange / purple / teal / pink / yellow` | — | — |

### 1.2. Spatiere (4pt scale)
`--space-1` (4) · `--space-2` (8) · `--space-3` (12) · `--space-4` (16) · `--space-5` (20) · `--space-6` (24) · `--space-8` (32) · `--space-10` (40) · `--space-12` (48) · `--space-16` (64).

### 1.3. Border radius
`--radius-sm` (6) · `--radius-md` (10) · `--radius-lg` (14) · `--radius-xl` (20) · `--radius-full` (9999).

### 1.4. Typography
- Font-family: `Inter` (Google Fonts) + fallback system-ui.
- Scale: `--text-xs` (11) → `--text-4xl` (36).
- Weights: `--fw-normal` (400) → `--fw-bold` (700).
- Line-heights: `--lh-tight` (1.25) → `--lh-relaxed` (1.625).

### 1.5. Elevation
Shadow ramp `--shadow-xs` → `--shadow-xl` + `--shadow-inner`. **Nou**: `--ring-focus`, `--ring-danger`, `--ring-success` pentru focus consistent.

### 1.6. Motion
`--transition-fast` (0.15s) · `--transition-base` (0.2s) · `--transition-slow` (0.3s) · `--transition-colors` (color/bg/border 0.2s).
`@media (prefers-reduced-motion: reduce)` dezactiveaza animatiile.

### 1.7. Z-index
`--z-base` (0) · `--z-raised` (10) · `--z-dropdown` (20) · `--z-sticky` (40) · `--z-overlay` (100) · `--z-modal` (200) · `--z-toast` (300).

---

## 2. Clase utilitare globale

Definite in `main.css` si disponibile fara import:

| Clasa | Rol |
|---|---|
| `.card` | Container elevat cu border, radius `lg`, shadow `sm`. Hover ridicat la `md`. |
| `.btn` + `.btn-primary` / `.btn-secondary` / `.btn-ghost` / `.btn-danger` | Variante de buton cu transform + shadow on hover. Minim 44px inaltime pentru touch. |
| **NEW** `.btn-success` / `.btn-outline` / `.btn-lg` | Variante adaugate in cadrul audit-ului. |
| `.badge` | Pill mic cu font semibold. |
| `.input` | Input uniform: 44px, border 1.5px, focus ring albastru. |
| **NEW** `.input.has-error` | Focus ring danger, margine rosie. |
| `.divider` | Linie de 1px, margine vertical-4. |
| `.skeleton` | Shimmer animat pentru loading. |
| **NEW** `.form-field` + `.form-hint` + `.form-error` | Wrapper pentru input cu label, hint si mesaj eroare. |
| **NEW** `.stepper` + `.stepper-item.active/.done` | Indicator pasi pentru fluxuri multi-step (checkout). |
| **NEW** `.chip` + `.chip.selected` | Selector pill (ex. tip bilet). |
| **NEW** `.alert` + `.alert-success/.alert-danger/.alert-info` | Mesaje inline. |
| **NEW** `.page` / `.page-narrow` / `.page-header` / `.page-title` / `.page-subtitle` | Layout container pentru ecrane narative (checkout, lista bilete). |

---

## 3. Componente reutilizabile (Vue SFC)

Pana la acest audit existau componente specifice (map, sidebar, trip planner) dar nu un set de componente de baza. Au fost adaugate urmatoarele primitive:

| Component | Fisier | Rol |
|---|---|---|
| `BaseModal` | `src/components/BaseModal.vue` | Modal cu backdrop + blur, close pe Escape, slot-uri `header`/`default`/`footer`. Inlocuieste dialog-urile ad-hoc din view-uri. |
| `FormField` | `src/components/FormField.vue` | Wrapper label + input (slot) + hint + error. Primeste slot-prop `hasError` pentru integrare cu inputs. |
| `CardInputForm` | `src/components/CardInputForm.vue` | Formular cont card cu formatare 4-4-4-4, validare Luhn, detectie brand (Visa/MC), card de test one-click. |
| `TicketCard` | `src/components/TicketCard.vue` | Reprezentare vizuala bilet (header gradient, grid date, footer QR). |

### Patternuri Vue

- `<script setup lang="ts">` + `defineProps` / `defineEmits` typed.
- Import-uri via alias `@/` → `src/`.
- Scoped CSS cu tokens globale (fara hard-coded hex in noile componente).
- Responsive: media queries la 520px / 768px.

---

## 4. Gap-uri identificate (audit)

1. **Dialog-uri duplicat**: `FavoritesView.vue` implementeaza un dialog propriu cu 200+ linii CSS. **Recomandare**: migrat la `BaseModal` in iteratie viitoare (follow-up, nu in scope-ul acestui PR — riscul de regressie vizuala cere testare aparte).
2. **Tokens de focus ring inconsistente**: inainte de audit, unele inputuri foloseau `box-shadow: 0 0 0 3px rgba(59,130,246,0.15)` hardcodat. **Aplicat**: `--ring-focus`, `--ring-danger`, `--ring-success` centralizate.
3. **Lipsa variante de buton**: `btn-success`, `btn-outline`, `btn-lg` nu existau desi erau necesare pentru CTA-uri in checkout / bilete. **Aplicat**.
4. **Lipsa clase pentru layout pagina**: ecranele inlocuiau `max-width` manual. **Aplicat**: `.page`, `.page-narrow`, `.page-header`, `.page-title`.
5. **Lipsa pattern pentru alert/mesaj inline**: fiecare view stila propriul mesaj de eroare. **Aplicat**: `.alert-success/-danger/-info/-warning`.
6. **Fara documentatie design system**: acest fisier (`DESIGN_SYSTEM.md`) este prima forma documentata.
7. **Skeleton-uri duplicate**: existau 4 implementari (`SkeletonLoader`, `ListSkeleton`, `MapSkeleton`, `StationCardSkeleton`) dar ultimele 3 nu erau importate nicaieri. **Aplicat**: sterse `ListSkeleton`, `MapSkeleton`, `StationCardSkeleton`. `SkeletonLoader.vue` ramane canonic cu `variant` prop.

---

## 4bis. Audit cantitativ (2026-04-21)

Score total: **58 / 100**. Puncte slabe:

| Categorie | Definit in tokens | Hardcodat in `.vue` | Top offender |
|---|---|---|---|
| Culori (hex/rgb) | 30+ tokens | ~474 literale in 30 fisiere | `MapView.vue` (128) |
| Spatiere (`px`) | 10-step 4pt scale | mii de valori raw | `Sidebar.vue` (216) |
| Border radius | 5 tokens | 179 literale in 27 fisiere | `Sidebar.vue` (19) |
| `#fff`/`#000`/`white`/`black` literal (risc dark mode) | `--text-inverse`, `--bg-primary` | 14 fisiere | `AdvancedTripPlanner.vue` (20) |
| Inline `style=` in template | — | 15 fisiere | `MapView.vue` (7) |

### Follow-up prioritizat (nu in scope-ul audit-ului curent)

| Prioritate | Actiune | Risc | Effort |
|---|---|---|---|
| P0 | Extras `MapView.vue` (3299 linii, 128 culori hardcodate) in `MapView` + `MapControls` + `MapMarkers` | **Mare** — feature central, fara teste automate | 4-8h |
| P1 | Migrat scoped styles din `Sidebar.vue` la tokens `--space-*` + `--radius-*` | Mediu | 2-3h |
| P1 | `FavoritesView.vue` dialog → `BaseModal` | Mediu | 1h |
| P2 | `.alert-warning` variant | — **Aplicat 2026-04-21** | — |
| P2 | Consolidare skeleton | — **Aplicat 2026-04-21** | — |
| P3 | Sizes `.input-sm` / `.input-lg` | Mic | 30min |

---

## 5. Accesibilitate (nota scurta)

- Contrastul token-urilor text peste background-uri trece WCAG AA (verificat vizual la bg-primary/secondary cu text-primary/secondary).
- Toate input-urile si butoanele au focus ring vizibil (`--ring-focus`).
- `BaseModal` include `role="dialog"`, `aria-modal="true"`, `aria-label`, Escape-to-close si blocare scroll body.
- `FormField` foloseste `role="alert"` pe mesajul de eroare.
- Bottom nav-ul are icon + etichetă text (nu doar iconite), min-width 44px.
- **Sugestii follow-up**: rulat `design:accessibility-review` pe `/tickets/checkout` pentru audit WCAG complet.

---

## 6. Cum se adauga un nou token / clasa

1. Adauga CSS variable in `:root` (light) si in `.dark` (dark mode) in `main.css`.
2. Daca este o clasa utilitara, plaseaza-o in sectiunea "NEW COMPONENT UTILITIES" din `main.css`.
3. Documenteaza in tabelele de mai sus din `DESIGN_SYSTEM.md`.
4. Pentru componente noi Vue: `src/components/Base<Name>.vue`, scoped CSS, tokens globale numai.

---

## 7. Cum se foloseste (exemplu)

```vue
<template>
  <div class="page page-narrow">
    <header class="page-header">
      <div>
        <h1 class="page-title">Titlu</h1>
        <p class="page-subtitle">Descriere</p>
      </div>
      <button class="btn btn-primary">CTA</button>
    </header>

    <FormField label="Email" :error="error" required>
      <input class="input" :class="{ 'has-error': !!error }" v-model="email" />
    </FormField>

    <div v-if="success" class="alert alert-success">Merge!</div>
  </div>
</template>
```
