<template>
  <div class="about-page">
    <!-- Header -->
    <div class="about-header">
      <span class="header-icon" aria-hidden="true"><BusFront :size="28" /></span>
      <h1 class="header-title">Tursib Tracker</h1>
      <p class="header-tagline">{{ t('appTagline') }}</p>
      <span class="version-badge">v1.0.0</span>
    </div>

    <div class="about-content">

      <!-- Despre -->
      <section class="about-section">
        <h2 class="section-title">{{ t('aboutAppTitle') }}</h2>
        <p class="section-text">{{ t('aboutAppText1') }}</p>
        <p class="section-text">{{ t('aboutAppText2') }}</p>
      </section>

      <!-- Funcționalități -->
      <section class="about-section">
        <h2 class="section-title">{{ t('features') }}</h2>
        <div class="features-grid">
          <div class="feature-card">
            <span class="feature-icon" aria-hidden="true"><MapIcon :size="18" /></span>
            <strong>{{ t('featureInteractiveMap') }}</strong>
            <span>{{ t('featureInteractiveMapDesc') }}</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon" aria-hidden="true"><Clock :size="18" /></span>
            <strong>{{ t('featureRealtime') }}</strong>
            <span>{{ t('featureRealtimeDesc') }}</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon" aria-hidden="true"><Route :size="18" /></span>
            <strong>{{ t('planning') }}</strong>
            <span>{{ t('featurePlanningDesc') }}</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon" aria-hidden="true"><Ticket :size="18" /></span>
            <strong>{{ t('featureDigitalTickets') }}</strong>
            <span>{{ t('featureDigitalTicketsDesc') }}</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon" aria-hidden="true"><Star :size="18" /></span>
            <strong>{{ t('favorites') }}</strong>
            <span>{{ t('featureFavoritesDesc') }}</span>
          </div>
          <div class="feature-card">
            <span class="feature-icon" aria-hidden="true"><BarChart3 :size="18" /></span>
            <strong>{{ t('statistics') }}</strong>
            <span>{{ t('featureStatisticsDesc') }}</span>
          </div>
        </div>
      </section>

      <!-- Info tehnic -->
      <section class="about-section">
        <h2 class="section-title">{{ t('technicalInfo') }}</h2>
        <div class="info-list">
          <div class="info-row">
            <span class="info-label">{{ t('version') }}</span>
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
            <span class="info-label">{{ t('map') }}</span>
            <span class="info-value">Leaflet + OpenStreetMap</span>
          </div>
          <div class="info-row">
            <span class="info-label">{{ t('gtfsData') }}</span>
            <span class="info-value">Tursib Sibiu</span>
          </div>
        </div>
      </section>

      <!-- Contact -->
      <section class="about-section">
        <h2 class="section-title">{{ t('resourcesSection') }}</h2>
        <div class="links-list">
          <a href="https://www.tursib.ro" target="_blank" rel="noopener" class="ext-link">
            <Globe :size="18" aria-hidden="true" />
            <span>tursib.ro — {{ t('officialSite') }}</span>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6M15 3h6v6M10 14L21 3"
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </a>
          <a href="https://www.tursib.ro/page/tarife" target="_blank" rel="noopener" class="ext-link">
            <Ticket :size="18" aria-hidden="true" />
            <span>{{ t('tursibFares') }}</span>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6M15 3h6v6M10 14L21 3"
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </a>
        </div>
      </section>

      <!-- Footer -->
      <div class="about-footer">
        <span>{{ t('madeWithLove') }}</span>
        <span class="footer-sep">·</span>
        <span>Tursib Tracker © 2025</span>
      </div>

    </div>
  </div>
</template>

<script setup lang="ts">
import { BarChart3, BusFront, Clock, Globe, Map as MapIcon, Route, Star, Ticket } from 'lucide-vue-next'
import { useLanguage } from '@/composables/useLanguage'
const { t } = useLanguage()
</script>

<style scoped>
.about-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
  padding: 20px 16px 32px;
}

.about-header {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
}

.header-icon {
  width: 56px;
  height: 56px;
  border-radius: 14px;
  background: var(--bg-inverse);
  color: var(--text-inverse);
  display: flex;
  align-items: center;
  justify-content: center;
}

.header-title {
  margin: 4px 0 0;
  font-size: 30px;
  font-weight: var(--fw-heavy);
  letter-spacing: -0.02em;
}

.header-tagline {
  margin: 0;
  color: var(--text-secondary);
}

.version-badge {
  padding: 3px 10px;
  border-radius: var(--radius-full);
  background: var(--bg-tertiary);
  font-size: 12px;
  font-weight: var(--fw-bold);
}

.about-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.about-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.section-title {
  margin: 0;
  font-family: var(--font-sans);
  font-size: 13px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.section-text {
  margin: 0;
  line-height: 1.6;
}

.features-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.feature-card {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 14px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  font-size: 13px;
  color: var(--text-secondary);
}

.feature-card strong {
  font-size: 15px;
  color: var(--text-primary);
}

.feature-icon {
  width: 32px;
  height: 32px;
  margin-bottom: 6px;
  border-radius: var(--radius-md);
  background: var(--bg-tertiary);
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
}

.info-list,
.links-list {
  display: flex;
  flex-direction: column;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  overflow: hidden;
}

.info-row {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 14px;
  border-bottom: 1px solid var(--border-subtle);
  font-size: 14px;
}

.info-row:last-child {
  border-bottom: none;
}

.info-label {
  color: var(--text-secondary);
}

.info-value {
  font-weight: var(--fw-semibold);
  text-align: right;
}

.ext-link {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 52px;
  padding: 0 14px;
  border-bottom: 1px solid var(--border-subtle);
  color: var(--text-primary);
  font-weight: var(--fw-semibold);
}

.ext-link:last-child {
  border-bottom: none;
}

.ext-link span {
  flex: 1;
}

.ext-link:hover {
  background: var(--bg-secondary);
  color: var(--text-primary);
}

.about-footer {
  display: flex;
  justify-content: center;
  gap: 6px;
  font-size: 13px;
  color: var(--text-secondary);
}
</style>
