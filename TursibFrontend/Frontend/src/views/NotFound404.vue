<template>
  <div class="not-found-page">
    <div class="not-found-content">
      <div class="error-code">404</div>
      <div class="bus-animation">
        <div class="bus">
          <div class="bus-body">
            <div class="bus-window"></div>
            <div class="bus-door"></div>
          </div>
          <div class="bus-wheels">
            <div class="wheel"></div>
            <div class="wheel"></div>
          </div>
        </div>
        <div class="road"></div>
      </div>
      
      <h1>Autobuzul s-a rătăcit! 🚌</h1>
      <p>Pagina pe care o cauți nu există sau a fost mutată.</p>
      
      <div class="actions">
        <button @click="goHome" class="btn-primary">
          🏠 Înapoi la Prima Pagină
        </button>
        <button @click="goBack" class="btn-secondary">
          ← Înapoi
        </button>
      </div>
      
      <div class="suggestions">
        <h3>Ai încercat să accesezi:</h3>
        <ul>
          <li>
            <router-link to="/trip-planner">🗺️ Planificator de Rute</router-link>
          </li>
          <li>
            <router-link to="/favorites">⭐ Favorite</router-link>
          </li>
          <li>
            <router-link to="/settings">⚙️ Setări</router-link>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

const goHome = () => {
  router.push('/')
}

const goBack = () => {
  if (window.history.length > 2) {
    router.back()
  } else {
    router.push('/')
  }
}
</script>

<style scoped>
.not-found-page {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
}

.not-found-content {
  text-align: center;
  max-width: 600px;
  color: white;
}

.error-code {
  font-size: 8rem;
  font-weight: 900;
  background: linear-gradient(135deg, #ffffff, #f0f0f0);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  margin-bottom: 24px;
  line-height: 1;
  text-shadow: 0 4px 20px rgba(0, 0, 0, 0.2);
}

/* Bus Animation */
.bus-animation {
  position: relative;
  height: 120px;
  margin: 32px 0;
  overflow: hidden;
}

.bus {
  position: absolute;
  left: -100px;
  animation: drive 8s linear infinite;
}

@keyframes drive {
  0% {
    left: -100px;
  }
  100% {
    left: calc(100% + 100px);
  }
}

.bus-body {
  position: relative;
  width: 80px;
  height: 50px;
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  border-radius: 12px 12px 8px 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.bus-window {
  position: absolute;
  width: 25px;
  height: 20px;
  background: rgba(255, 255, 255, 0.9);
  border-radius: 4px;
  top: 8px;
  left: 28px;
}

.bus-door {
  position: absolute;
  width: 15px;
  height: 30px;
  background: rgba(255, 255, 255, 0.6);
  border-radius: 2px;
  bottom: 0;
  right: 8px;
}

.bus-wheels {
  position: absolute;
  bottom: -8px;
  width: 100%;
  display: flex;
  justify-content: space-between;
  padding: 0 12px;
}

.wheel {
  width: 16px;
  height: 16px;
  background: #1e293b;
  border-radius: 50%;
  border: 3px solid #64748b;
  animation: rotate 0.5s linear infinite;
}

@keyframes rotate {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

.road {
  position: absolute;
  bottom: 0;
  width: 100%;
  height: 4px;
  background: repeating-linear-gradient(
    90deg,
    rgba(255, 255, 255, 0.5) 0px,
    rgba(255, 255, 255, 0.5) 20px,
    transparent 20px,
    transparent 40px
  );
}

/* Content */
h1 {
  font-size: 2rem;
  font-weight: 700;
  margin: 24px 0 16px 0;
}

p {
  font-size: 1.1rem;
  opacity: 0.9;
  margin: 0 0 32px 0;
}

/* Actions */
.actions {
  display: flex;
  gap: 16px;
  justify-content: center;
  margin-bottom: 48px;
  flex-wrap: wrap;
}

.btn-primary,
.btn-secondary {
  padding: 14px 28px;
  border-radius: 12px;
  font-weight: 600;
  font-size: 1rem;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
}

.btn-primary {
  background: white;
  color: #667eea;
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(255, 255, 255, 0.3);
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.2);
  color: white;
  backdrop-filter: blur(8px);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.3);
}

/* Suggestions */
.suggestions {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(8px);
  border-radius: 16px;
  padding: 24px;
  border: 1px solid rgba(255, 255, 255, 0.2);
}

.suggestions h3 {
  font-size: 1rem;
  margin: 0 0 16px 0;
  opacity: 0.9;
}

.suggestions ul {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.suggestions li a {
  display: block;
  padding: 12px 16px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: white;
  text-decoration: none;
  transition: all 0.2s;
}

.suggestions li a:hover {
  background: rgba(255, 255, 255, 0.2);
  transform: translateX(4px);
}

/* Responsive */
@media (max-width: 640px) {
  .error-code {
    font-size: 5rem;
  }

  h1 {
    font-size: 1.5rem;
  }

  p {
    font-size: 1rem;
  }

  .actions {
    flex-direction: column;
  }

  .btn-primary,
  .btn-secondary {
    width: 100%;
  }
}
</style>
