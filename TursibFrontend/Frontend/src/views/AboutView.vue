<template>
  <div class="about-page">
    <!-- Header -->
    <div class="about-header">
      <div class="header-icon">🚌</div>
      <h1 class="header-title">Tursib Tracker</h1>
      <p class="header-tagline">Aplicație web pentru urmărirea în timp real a autobuzelor</p>
      <span class="version-badge">v1.0.0</span>
    </div>

    <div class="about-content">

      <!-- Despre -->
      <section class="about-section">
        <h2 class="section-title">Despre aplicație</h2>
        <p class="section-text">
          Tursib Tracker este o aplicație web modernă care îți permite să urmărești autobuzele
          Tursib din Sibiu în timp real, să planifici călătoriile și să cumperi bilete digital —
          totul din browser, fără instalare.
        </p>
        <p class="section-text">
          Aplicația folosește datele GTFS oficiale ale Tursib pentru a afișa trasee, orare și
          estimări de sosire actualizate.
        </p>
      </section>

      <!-- Funcționalități -->
      <section class="about-section">
        <h2 class="section-title">Funcționalități</h2>
        <div class="features-grid">
          <div class="feature-card">
            <span class="feature-icon">🗺️</span>
            <strong>Hartă interactivă</strong>
            <span>Stații și trasee pe OpenStreetMap</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon">⏱️</span>
            <strong>Timp real</strong>
            <span>ETA și orare actualizate live</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon">📅</span>
            <strong>Planificare</strong>
            <span>Calculează ruta optimă</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon">🎫</span>
            <strong>Bilete digitale</strong>
            <span>Cumpără direct din aplicație</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon">⭐</span>
            <strong>Favorite</strong>
            <span>Salvează stațiile preferate</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon">📊</span>
            <strong>Statistici</strong>
            <span>Istoricul călătoriilor tale</span>
          </div>
        </div>
      </section>

      <!-- Info tehnic -->
      <section class="about-section">
        <h2 class="section-title">Informații tehnice</h2>
        <div class="info-list">
          <div class="info-row">
            <span class="info-label">Versiune</span>
            <span class="info-value">1.0.0</span>
          </div>
          <div class="info-row">
            <span class="info-label">Frontend</span>
            <span class="info-value">Vue 3 + TypeScript</span>
          </div>
          <div class="info-row">
            <span class="info-label">Backend</span>
            <span class="info-value">ASP.NET Core 9</span>
          </div>
          <div class="info-row">
            <span class="info-label">Hartă</span>
            <span class="info-value">Leaflet + OpenStreetMap</span>
          </div>
          <div class="info-row">
            <span class="info-label">Date GTFS</span>
            <span class="info-value">Tursib Sibiu</span>
          </div>
        </div>
      </section>

      <!-- Contact -->
      <section class="about-section">
        <h2 class="section-title">Resurse</h2>
        <div class="links-list">
          <a href="https://www.tursib.ro" target="_blank" rel="noopener" class="ext-link">
            <span>🌐</span>
            <span>tursib.ro — site oficial</span>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
              <path d="M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6M15 3h6v6M10 14L21 3"
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </a>
          <a href="https://www.tursib.ro/page/tarife" target="_blank" rel="noopener" class="ext-link">
            <span>🎫</span>
            <span>Tarife Tursib 2025</span>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
              <path d="M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6M15 3h6v6M10 14L21 3"
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </a>
        </div>
      </section>

      <!-- Footer -->
      <div class="about-footer">
        <span>Made with ♥ în Sibiu 🇷🇴</span>
        <span class="footer-sep">·</span>
        <span>Tursib Tracker © 2025</span>
      </div>

    </div>
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
.about-page {
  min-height: 100%;
  background: var(--bg-secondary);
  padding-bottom: var(--space-8);
}

/* Header */
.about-header {
  background: var(--gradient-primary);
  padding: var(--space-10) var(--space-6) var(--space-8);
  text-align: center;
  color: white;
}
.header-icon {
  font-size: 56px;
  margin-bottom: var(--space-3);
  display: block;
  filter: drop-shadow(0 4px 8px rgba(0,0,0,0.2));
}
.header-title {
  font-size: var(--text-3xl);
  font-weight: 800;
  color: white;
  margin: 0 0 var(--space-2) 0;
}
.header-tagline {
  font-size: var(--text-sm);
  color: rgba(255,255,255,0.85);
  margin: 0 0 var(--space-4) 0;
  max-width: 340px;
  margin-left: auto;
  margin-right: auto;
  line-height: var(--lh-relaxed);
}
.version-badge {
  display: inline-block;
  background: rgba(255,255,255,0.2);
  color: white;
  font-size: var(--text-xs);
  font-weight: var(--fw-semibold);
  padding: 3px 10px;
  border-radius: var(--radius-full);
  border: 1px solid rgba(255,255,255,0.3);
}

/* Content */
.about-content {
  padding: var(--space-6) var(--space-4);
  max-width: 560px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
}

/* Section */
.about-section {
  background: var(--bg-elevated);
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  box-shadow: var(--shadow-xs);
}
.section-title {
  font-size: var(--text-md);
  font-weight: var(--fw-semibold);
  color: var(--text-primary);
  margin: 0 0 var(--space-3) 0;
}
.section-text {
  font-size: var(--text-sm);
  color: var(--text-secondary);
  line-height: var(--lh-relaxed);
  margin-bottom: var(--space-2);
}
.section-text:last-child { margin-bottom: 0; }

/* Features grid */
.features-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-3);
}
.feature-card {
  display: flex;
  flex-direction: column;
  gap: 4px;
  background: var(--bg-secondary);
  border-radius: var(--radius-md);
  padding: var(--space-3);
  border: 1px solid var(--border-primary);
}
.feature-icon {
  font-size: 22px;
  margin-bottom: 2px;
}
.feature-card strong {
  font-size: var(--text-sm);
  font-weight: var(--fw-semibold);
  color: var(--text-primary);
}
.feature-card span {
  font-size: var(--text-xs);
  color: var(--text-secondary);
  line-height: 1.3;
}

/* Info list */
.info-list {
  display: flex;
  flex-direction: column;
}
.info-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 0;
  border-bottom: 1px solid var(--bg-tertiary);
  gap: var(--space-3);
}
.info-row:last-child { border-bottom: none; }
.info-label {
  font-size: var(--text-sm);
  color: var(--text-secondary);
}
.info-value {
  font-size: var(--text-sm);
  font-weight: var(--fw-medium);
  color: var(--text-primary);
}

/* External links */
.links-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}
.ext-link {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-primary);
  color: var(--accent-primary);
  font-size: var(--text-sm);
  font-weight: var(--fw-medium);
  text-decoration: none;
  transition: var(--transition-base);
}
.ext-link:hover {
  background: var(--accent-primary-soft);
  border-color: var(--accent-primary);
  text-decoration: none;
}
.ext-link span:nth-child(2) {
  flex: 1;
}
.ext-link svg {
  flex-shrink: 0;
  color: var(--text-tertiary);
}

/* Footer */
.about-footer {
  text-align: center;
  font-size: var(--text-xs);
  color: var(--text-tertiary);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  padding: var(--space-2) 0;
}
.footer-sep { color: var(--border-secondary); }

@media (max-width: 360px) {
  .features-grid { grid-template-columns: 1fr; }
}
</style>
