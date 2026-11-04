import type { Component } from 'vue'
import {
  Briefcase, BookOpen, Coffee, Dumbbell, GraduationCap, Home, Hospital, MapPin, Mountain, Music,
  ShoppingCart, Theater, TreePine, Umbrella, Utensils, Church
} from 'lucide-vue-next'

// Iconițele favoritelor sunt salvate ca șir de caractere în baza de date.
// Noile favorite folosesc chei („home”, „cafe”...); cele vechi au emoji, pe care le traducem la afișare.

export interface FavoriteIconOption {
  key: string
  component: Component
}

export const FAVORITE_ICONS: readonly FavoriteIconOption[] = [
  { key: 'pin', component: MapPin },
  { key: 'home', component: Home },
  { key: 'work', component: Briefcase },
  { key: 'school', component: GraduationCap },
  { key: 'hospital', component: Hospital },
  { key: 'shop', component: ShoppingCart },
  { key: 'theater', component: Theater },
  { key: 'church', component: Church },
  { key: 'gym', component: Dumbbell },
  { key: 'food', component: Utensils },
  { key: 'cafe', component: Coffee },
  { key: 'library', component: BookOpen },
  { key: 'music', component: Music },
  { key: 'park', component: TreePine },
  { key: 'beach', component: Umbrella },
  { key: 'mountain', component: Mountain }
]

const LEGACY_EMOJI: Record<string, string> = {
  '🏠': 'home', '💼': 'work', '🏫': 'school', '🏥': 'hospital', '🛒': 'shop', '🎭': 'theater',
  '⛪': 'church', '🏋️': 'gym', '🍽️': 'food', '☕': 'cafe', '📚': 'library', '🎵': 'music',
  '🌳': 'park', '🏖️': 'beach', '⛰️': 'mountain', '📍': 'pin', '🚏': 'pin'
}

/** Cheia normalizată a unei iconițe (acceptă și emoji vechi). */
export function favoriteIconKey(icon: string | undefined, type?: string): string {
  if (icon && FAVORITE_ICONS.some(o => o.key === icon)) return icon
  if (icon && LEGACY_EMOJI[icon]) return LEGACY_EMOJI[icon]!
  if (type === 'home') return 'home'
  if (type === 'work') return 'work'
  return 'pin'
}

export function favoriteIconComponent(icon: string | undefined, type?: string): Component {
  const key = favoriteIconKey(icon, type)
  return FAVORITE_ICONS.find(o => o.key === key)!.component
}
